using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class LocalSpawner : NetworkBehaviour
{

    public static LocalSpawner Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    [Header("Spawning Configuration")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnLocations;


    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private Dictionary<ulong, GameObject> spawnedPlayers = new Dictionary<ulong, GameObject>();
    private int currentSpawnIndex = 0;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"━━━━ [LocalSpawner] OnNetworkSpawn START ━━━━");
        Debug.Log($"[LocalSpawner] IsServer={IsServer}, IsClient={IsClient}, IsHost={IsHost}");
        
        if (!IsServer)
        {
            Debug.Log($"[LocalSpawner] Not server, exiting OnNetworkSpawn");
            return;
        }

        if (!HasValidSpawnPoints())
        {
            Debug.LogError("[LocalSpawner] Missing spawn locations; cannot spawn players.");
            return;
        }

        Debug.Log("[LocalSpawner] Initialized on Server");

        // Subscribe to scene events to know when scene load is complete
        NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
        
        // Also subscribe to connection callbacks for late joiners
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        
        Debug.Log($"━━━━ [LocalSpawner] OnNetworkSpawn END ━━━━");
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // Only spawn when scene load is COMPLETE (all clients ready)
        if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
        {
            Debug.Log($"[LocalSpawner] Scene load completed, spawning players now");
            
            int connectedCount = NetworkManager.Singleton.ConnectedClients.Count;
            Debug.Log($"[LocalSpawner] Connected clients count: {connectedCount}");

            // Spawn for all connected clients
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                Debug.Log($"[LocalSpawner] → Spawning player for ClientId={client.ClientId}");
                SpawnPlayerForClient(client.ClientId);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (debugMode)
            Debug.Log($"Client {clientId} connected. Spawning player...");

        SpawnPlayerForClient(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (debugMode)
            Debug.Log($"Client {clientId} disconnected. Cleaning up player...");

        DespawnPlayerForClient(clientId);
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        Debug.Log($"[SpawnPlayer] START for ClientId={clientId}");
        
        if (!IsServer)
        {
            Debug.LogWarning($"[SpawnPlayer] Not server, aborting");
            return;
        }
        
        if (playerPrefab == null)
        {
            Debug.LogError("[SpawnPlayer] Player prefab is not assigned!");
            return;
        }

        if (!HasValidSpawnPoints())
        {
            Debug.LogError("[SpawnPlayer] No spawn locations assigned!");
            return;
        }

        if (spawnedPlayers.ContainsKey(clientId))
        {
            Debug.LogWarning($"[SpawnPlayer] Player for client {clientId} already exists!");
            return;
        }

        // Get spawn position
        Transform spawnLocation = GetNextSpawnLocation();
        Vector3 spawnPosition = spawnLocation.position;
        Quaternion spawnRotation = spawnLocation.rotation;
        Debug.Log($"[SpawnPlayer] Spawn position: {spawnPosition}, rotation: {spawnRotation}");

        // Instantiate player prefab
        GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, spawnRotation);
        Debug.Log($"[SpawnPlayer] Instantiated GameObject: {playerInstance.name}");

        // Get NetworkObject and spawn with ownership
        NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            Debug.Log($"[SpawnPlayer] Found NetworkObject, calling SpawnAsPlayerObject for ClientId={clientId}");
            
            // networkObject.SpawnWithOwnership(clientId);
            networkObject.SpawnAsPlayerObject(clientId, true);
            
            Debug.Log($"[Spawner] client={clientId} PlayerObjectNull={NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject==null}");
            Debug.Log($"[SpawnPlayer] NetworkObject spawned! IsSpawned={networkObject.IsSpawned}, OwnerClientId={networkObject.OwnerClientId}");

            spawnedPlayers[clientId] = playerInstance;

            if (debugMode)
                Debug.Log($"[SpawnPlayer] ✅ COMPLETE for ClientId={clientId} at {spawnPosition}");
        }
        else
        {
            Debug.LogError("[SpawnPlayer] Player prefab must have a NetworkObject component!");
            Destroy(playerInstance);
        }
    }
    private void DespawnPlayerForClient(ulong clientId)
    {
        if (spawnedPlayers.TryGetValue(clientId, out GameObject playerInstance))
        {
            if (playerInstance != null)
            {
                NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                }
            }

            spawnedPlayers.Remove(clientId);

            if (debugMode)
                Debug.Log($"Player despawned for client {clientId}");
        }
    }

    private Transform GetNextSpawnLocation()
    {
        Transform spawnLocation = spawnLocations[currentSpawnIndex];
        currentSpawnIndex = (currentSpawnIndex + 1) % spawnLocations.Length;
        return spawnLocation;
    }

    // Public methods for external access
    public GameObject GetPlayerForClient(ulong clientId)
    {
        spawnedPlayers.TryGetValue(clientId, out GameObject player);
        return player;
    }

    public int GetSpawnedPlayerCount()
    {
        return spawnedPlayers.Count;
    }

    private bool HasValidSpawnPoints()
    {
        return spawnLocations != null && spawnLocations.Length > 0;
    }

    // Validation
    private void OnValidate()
    {
        if (playerPrefab != null && playerPrefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogWarning("Player prefab should have a NetworkObject component!");
        }
    }

    public Transform GetSpawnForIndex(int index)
    {
        if (spawnLocations == null || spawnLocations.Length == 0) return null;
        index = Mathf.Clamp(index, 0, spawnLocations.Length - 1);
        return spawnLocations[index];
    }

}

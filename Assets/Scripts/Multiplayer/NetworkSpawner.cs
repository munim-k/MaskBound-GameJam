using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using TMPro;

public class NetworkSpawner : NetworkBehaviour
{
    [Header("Match Rules")]
    [SerializeField] private int minPlayers = 2;
    [SerializeField] private int maxPlayers = 3;
    [SerializeField] private float matchmakingTimeout = 50f;

    [Header("Scenes")]

    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string timeoutSceneName = "MainMenu";



    [Header("UI")]
    [SerializeField] private GameObject textBox;
    [SerializeField] private GameObject timerText;
    [SerializeField] private GameObject playersJoinedText;

    private TextMeshProUGUI timerTMP;
    private TextMeshProUGUI playersTMP;

    private float elapsedTime;
    private bool timerActive;
    private bool uploadPhaseStarted;
    private bool initRan;

    private void Awake()
    {
        if (timerText != null)
            timerTMP = timerText.GetComponent<TextMeshProUGUI>();

        if (playersJoinedText != null)
            playersTMP = playersJoinedText.GetComponent<TextMeshProUGUI>();
    }

    public override void OnNetworkSpawn()
    {
        Init();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void Init()
    {
        if (initRan)
        {
            Debug.Log("[NetworkSpawner] Init already ran, skipping");
            return;
        }
        
        initRan = true;
        Debug.Log($"[NetworkSpawner] Initializing... IsServer={IsServer}");

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            Debug.Log("[NetworkSpawner] ✅ Registered connection callbacks (auto-start at 3 players)");
        }
    }

    public void OnJoinPressed()
    {
        timerActive = true;
        elapsedTime = 0f;

        if (textBox != null) textBox.SetActive(true);
        if (timerText != null) timerText.SetActive(true);
    }

    private void Update()
    {
        if (!timerActive || uploadPhaseStarted) return;

        elapsedTime += Time.deltaTime;
        UpdateUI();

        if (elapsedTime >= matchmakingTimeout)
        {
            HandleTimeout();
        }
    }

    private void UpdateUI()
    {
        int connected = GetConnectedPlayers();

        if (playersTMP != null)
            playersTMP.text = $"Players: {connected}/{maxPlayers}";

        if (timerTMP != null)
        {
            int remaining = Mathf.CeilToInt(matchmakingTimeout - elapsedTime);
            timerTMP.text = $"Finding Match: {remaining}s";
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        int connectedCount = GetConnectedPlayers();
        Debug.Log($"[NetworkSpawner] ✅ Client connected: {clientId}, Total players: {connectedCount}/{maxPlayers}");

        // Guard added (extra safety)
        if (uploadPhaseStarted)
        {
            Debug.LogWarning($"[NetworkSpawner] Upload phase already started, ignoring connection");
            return;
        }

        // Auto-start upload ONLY when lobby is full (3 players)
        if (connectedCount >= maxPlayers)
        {
            Debug.Log($"[NetworkSpawner] 🎮 LOBBY FULL ({connectedCount}/{maxPlayers}) - AUTO-STARTING GAME!");
            StartUploadPhase();
        }
        else
        {
            Debug.Log($"[NetworkSpawner] ⏳ Waiting for more players... ({connectedCount}/{maxPlayers})");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
    }

    private void StartUploadPhase()
    {
        if (uploadPhaseStarted)
        {
            Debug.LogWarning("[NetworkSpawner] StartUploadPhase called but already started!");
            return;
        }
        
        if (!IsServer)
        {
            Debug.LogError("[NetworkSpawner] StartUploadPhase called on non-server!");
            return;
        }

        uploadPhaseStarted = true;
        timerActive = false;

        Debug.Log("[NetworkSpawner] 🚀 Starting upload phase...");

        if (textBox != null) textBox.SetActive(false);
        if (timerText != null) timerText.SetActive(false);

        // Delegates UI + flow control to UIManager
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            Debug.Log("[NetworkSpawner] ✅ Found UIManager, calling StartUploadPhaseServerRpc");
            uiManager.StartUploadPhaseServerRpc();
        }
        else
        {
            Debug.LogError("[NetworkSpawner] ❌ NO UIManager FOUND! Upload phase cannot start!");
        }
    }

    private void HandleTimeout()
    {
        Debug.Log("Matchmaking timed out.");

        timerActive = false;

        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(timeoutSceneName);
        // NetworkManager.Singleton.SceneManager.LoadScene(timeoutSceneName);

    }

    private int GetConnectedPlayers()
    {
        return NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ConnectedClients.Count
            : 0;
    }

    public void OnStartGamePressed()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("Cannot start game: NetworkManager missing.");
            return;
        }

        Debug.Log("Loading game scene: " + gameSceneName);
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
}

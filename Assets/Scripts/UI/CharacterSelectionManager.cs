using UnityEngine;
using Unity.Netcode;
using MaskBound.Core.Enums;
using System.Collections.Generic;
using System.Linq;

namespace MaskBound.UI
{
    /// <summary>
    /// Network-synchronized character selection manager for lobby.
    /// Tracks player affinity selections and confirmation status.
    /// Prevents duplicate selections across all clients.
    /// </summary>
    public class CharacterSelectionManager : NetworkBehaviour
    {
        public static CharacterSelectionManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private UIManager uiManager;

        // Network state: clientId -> selected affinity (0=None, 1=Orc, 2=ScorpionMan, 3=Gargoyle)
        private NetworkList<ClientSelectionData> clientSelections = new NetworkList<ClientSelectionData>();
        
        // Track confirmation status
        private NetworkList<ulong> confirmedClients = new NetworkList<ulong>();

        // Events for UI updates
        public event System.Action<Dictionary<ulong, EnemyFamily>> OnSelectionsChanged;
        public event System.Action<HashSet<ulong>> OnConfirmationsChanged;

        private struct ClientSelectionData : INetworkSerializable, System.IEquatable<ClientSelectionData>
        {
            public ulong ClientId;
            public int SelectedAffinity; // -1=None, 0=Orc, 1=ScorpionMan, 2=Gargoyle (raw enum values)

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref ClientId);
                serializer.SerializeValue(ref SelectedAffinity);
            }

            public bool Equals(ClientSelectionData other)
            {
                return ClientId == other.ClientId && SelectedAffinity == other.SelectedAffinity;
            }

            public override bool Equals(object obj)
            {
                return obj is ClientSelectionData other && Equals(other);
            }

            public override int GetHashCode()
            {
                return System.HashCode.Combine(ClientId, SelectedAffinity);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            // Subscribe to NetworkList changes
            clientSelections.OnListChanged += OnSelectionsListChanged;
            confirmedClients.OnListChanged += OnConfirmedListChanged;

            Debug.Log("[CharacterSelection] NetworkSpawned - waiting for InitializeClients() call");
        }

        /// <summary>
        /// Initialize client selections when character selection panel is shown
        /// Called by UIManager when showing the panel
        /// </summary>
        public void InitializeClients()
        {
            if (!IsServer) return;

            // Clear any existing selections
            clientSelections.Clear();

            // Initialize selections for all currently connected clients
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                clientSelections.Add(new ClientSelectionData
                {
                    ClientId = client.Key,
                    SelectedAffinity = -1 // None (-1 to avoid conflict with Orc=0)
                });
            }

            Debug.Log($"[CharacterSelection] ✅ Initialized for {clientSelections.Count} clients");
        }

        public override void OnNetworkDespawn()
        {
            if (clientSelections != null)
                clientSelections.OnListChanged -= OnSelectionsListChanged;
            if (confirmedClients != null)
                confirmedClients.OnListChanged -= OnConfirmedListChanged;
        }

        /// <summary>
        /// Client requests to select an affinity
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SelectAffinityServerRpc(int affinity, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            EnemyFamily requestedAffinity = (EnemyFamily)affinity;

            // Check if this affinity is CONFIRMED by another player
            bool isConfirmedByOther = false;
            foreach (ulong confirmedClientId in confirmedClients)
            {
                if (confirmedClientId == clientId) continue; // Skip self
                
                var confirmedSelection = GetClientSelection(confirmedClientId);
                if (confirmedSelection == (int)requestedAffinity) // Cast enum to int for comparison
                {
                    isConfirmedByOther = true;
                    break;
                }
            }

            if (isConfirmedByOther)
            {
                Debug.LogWarning($"[CharacterSelection] Client {clientId} tried to select CONFIRMED affinity: {requestedAffinity}");
                return;
            }

            // If another player has selected (but NOT confirmed) this affinity, unselect them
            for (int i = 0; i < clientSelections.Count; i++)
            {
                var data = clientSelections[i];
                if (data.ClientId != clientId && data.SelectedAffinity == affinity)
                {
                    // Unselect the other player
                    data.SelectedAffinity = -1; // None
                    clientSelections[i] = data;
                    Debug.Log($"[CharacterSelection] Client {data.ClientId}'s unconfirmed selection of {requestedAffinity} was overridden by Client {clientId}");
                }
            }

            // Update this client's selection
            for (int i = 0; i < clientSelections.Count; i++)
            {
                var data = clientSelections[i];
                if (data.ClientId == clientId)
                {
                    data.SelectedAffinity = affinity;
                    clientSelections[i] = data;
                    Debug.Log($"[CharacterSelection] Client {clientId} selected {requestedAffinity}");
                    break;
                }
            }

            // Remove confirmation if they change selection
            for (int i = confirmedClients.Count - 1; i >= 0; i--)
            {
                if (confirmedClients[i] == clientId)
                {
                    confirmedClients.RemoveAt(i);
                    Debug.Log($"[CharacterSelection] Client {clientId} unconfirmed due to selection change");
                    break;
                }
            }
        }

        /// <summary>
        /// Client confirms their selection
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ConfirmSelectionServerRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Validate client has made a selection (-1 means None)
            bool hasSelection = false;
            foreach (var data in clientSelections)
            {
                if (data.ClientId == clientId && data.SelectedAffinity >= 0) // >= 0 means a valid enum value
                {
                    hasSelection = true;
                    break;
                }
            }

            if (!hasSelection)
            {
                Debug.LogWarning($"[CharacterSelection] Client {clientId} tried to confirm without selection!");
                return;
            }

            // Add to confirmed list if not already
            if (!confirmedClients.Contains(clientId))
            {
                confirmedClients.Add(clientId);
                Debug.Log($"[CharacterSelection] Client {clientId} confirmed selection");
            }

            // Check if all clients confirmed
            if (IsAllConfirmed())
            {
                Debug.Log("[CharacterSelection] ✅ All clients confirmed! Storing selections and notifying UIManager");
                StoreSelectionsForGameScene();
                uiManager?.OnAllCharactersConfirmed();
            }
        }

        /// <summary>
        /// Get list of available (unconfirmed) affinities
        /// Only CONFIRMED selections block availability
        /// </summary>
        public List<EnemyFamily> GetAvailableAffinities()
        {
            var confirmedAffinities = new HashSet<EnemyFamily>();

            // Only count CONFIRMED selections as taken
            foreach (ulong confirmedClientId in confirmedClients)
            {
                var selection = GetClientSelection(confirmedClientId);
                if (selection >= 0) // Valid enum value (not -1/None)
                {
                    confirmedAffinities.Add((EnemyFamily)selection);
                }
            }

            var all = new List<EnemyFamily> { EnemyFamily.Orc, EnemyFamily.ScorpionMan, EnemyFamily.Gargoyle };
            return all.Where(a => !confirmedAffinities.Contains(a)).ToList();
        }

        /// <summary>
        /// Check if specific affinity is CONFIRMED by another client
        /// Unconfirmed selections don't block availability
        /// </summary>
        public bool IsAffinityTaken(EnemyFamily affinity)
        {
            foreach (ulong confirmedClientId in confirmedClients)
            {
                var selection = GetClientSelection(confirmedClientId);
                if (selection == (int)affinity) // Cast enum to int for comparison
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get current selection for a specific client
        /// </summary>
        public int GetClientSelection(ulong clientId)
        {
            foreach (var data in clientSelections)
            {
                if (data.ClientId == clientId)
                {
                    return data.SelectedAffinity;
                }
            }
            return -1; // None
        }

        /// <summary>
        /// Check if client has confirmed
        /// </summary>
        public bool IsClientConfirmed(ulong clientId)
        {
            return confirmedClients.Contains(clientId);
        }

        /// <summary>
        /// Check if all clients have confirmed
        /// </summary>
        public bool IsAllConfirmed()
        {
            if (confirmedClients.Count != NetworkManager.Singleton.ConnectedClients.Count)
                return false;

            // Verify all confirmed clients have valid selections
            foreach (ulong clientId in confirmedClients)
            {
                bool hasValidSelection = false;
                foreach (var data in clientSelections)
                {
                    if (data.ClientId == clientId && data.SelectedAffinity >= 0) // Valid enum value
                    {
                        hasValidSelection = true;
                        break;
                    }
                }
                if (!hasValidSelection)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Store selections in PlayerAffinity static dictionary for game scene
        /// </summary>
        private void StoreSelectionsForGameScene()
        {
            if (!IsServer) return;

            MaskBound.Player.PlayerAffinity.SelectedAffinities.Clear();

            foreach (var data in clientSelections)
            {
                if (data.SelectedAffinity >= 0) // Valid enum value
                {
                    MaskBound.Player.PlayerAffinity.SelectedAffinities[data.ClientId] = (EnemyFamily)data.SelectedAffinity;
                    Debug.Log($"[CharacterSelection] Stored selection for client {data.ClientId}: {(EnemyFamily)data.SelectedAffinity}");
                }
            }
        }

        /// <summary>
        /// Get count of confirmed clients
        /// </summary>
        public int GetConfirmedCount()
        {
            return confirmedClients.Count;
        }

        /// <summary>
        /// Get total client count
        /// </summary>
        public int GetTotalClientCount()
        {
            return NetworkManager.Singleton.ConnectedClients.Count;
        }

        // Event handlers for NetworkList changes
        private void OnSelectionsListChanged(NetworkListEvent<ClientSelectionData> changeEvent)
        {
            // Convert to dictionary for event
            var selections = new Dictionary<ulong, EnemyFamily>();
            foreach (var data in clientSelections)
            {
                selections[data.ClientId] = (EnemyFamily)data.SelectedAffinity;
            }
            OnSelectionsChanged?.Invoke(selections);
        }

        private void OnConfirmedListChanged(NetworkListEvent<ulong> changeEvent)
        {
            // Convert NetworkList to HashSet manually (NetworkList doesn't implement IEnumerable properly)
            var confirmed = new HashSet<ulong>();
            foreach (var clientId in confirmedClients)
            {
                confirmed.Add(clientId);
            }
            OnConfirmationsChanged?.Invoke(confirmed);
        }

        /// <summary>
        /// Handle client disconnect - free up their selection
        /// </summary>
        public void RemoveClient(ulong clientId)
        {
            if (!IsServer) return;

            // Remove selection
            for (int i = clientSelections.Count - 1; i >= 0; i--)
            {
                if (clientSelections[i].ClientId == clientId)
                {
                    clientSelections.RemoveAt(i);
                    break;
                }
            }

            // Remove confirmation
            for (int i = confirmedClients.Count - 1; i >= 0; i--)
            {
                if (confirmedClients[i] == clientId)
                {
                    confirmedClients.RemoveAt(i);
                    break;
                }
            }

            Debug.Log($"[CharacterSelection] Removed client {clientId}");
        }
    }
}
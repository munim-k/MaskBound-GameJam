using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class MaskAuthority : NetworkBehaviour
{
    public static MaskAuthority Instance;

    // Server-side ownership tracking: MaskType -> Client ID
    private Dictionary<MaskType, ulong> maskOwners = new();
    
    // Track which masks have been assigned (for initial distribution)
    private Queue<MaskType> availableMasks = new();
    private int assignmentIndex = 0;

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
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        // Initialize available masks queue in deterministic order
        availableMasks.Clear();
        availableMasks.Enqueue(MaskType.Fire);
        availableMasks.Enqueue(MaskType.Lightning);
        availableMasks.Enqueue(MaskType.Earth);
        
        Debug.Log("[MaskAuthority] Initialized on server with 3 available masks");
    }

    /// <summary>
    /// Assigns the next available mask to a player on initial spawn.
    /// Server-only. Returns the assigned mask.
    /// </summary>
    public MaskType AssignInitialMask(ulong clientId)
    {
        if (!IsServer)
        {
            Debug.LogError("[MaskAuthority] AssignInitialMask called on client!");
            return MaskType.Fire;
        }

        // Check if player already has a mask
        foreach (var kvp in maskOwners)
        {
            if (kvp.Value == clientId)
            {
                Debug.LogWarning($"[MaskAuthority] Client {clientId} already owns {kvp.Key}");
                return kvp.Key;
            }
        }

        // Assign next mask in sequence
        MaskType[] maskSequence = { MaskType.Fire, MaskType.Lightning, MaskType.Earth };
        MaskType assignedMask = maskSequence[assignmentIndex % maskSequence.Length];
        assignmentIndex++;

        // Validate mask isn't already owned
        if (maskOwners.ContainsKey(assignedMask))
        {
            Debug.LogError($"[MaskAuthority] CRITICAL: Attempted to assign {assignedMask} but it's already owned by client {maskOwners[assignedMask]}!");
            
            // Emergency: find any unowned mask
            foreach (MaskType mask in maskSequence)
            {
                if (!maskOwners.ContainsKey(mask))
                {
                    assignedMask = mask;
                    Debug.LogWarning($"[MaskAuthority] Emergency reassignment: gave {mask} to client {clientId}");
                    break;
                }
            }
        }

        maskOwners[assignedMask] = clientId;
        Debug.Log($"[MaskAuthority] Assigned {assignedMask} to client {clientId}");
        
        return assignedMask;
    }

    /// <summary>
    /// Get the mask currently owned by a specific player.
    /// Returns null if player doesn't own any mask.
    /// </summary>
    public MaskType? GetMaskOfPlayer(ulong clientId)
    {
        foreach (var kvp in maskOwners)
        {
            if (kvp.Value == clientId)
                return kvp.Key;
        }

        Debug.LogWarning($"[MaskAuthority] GetMaskOfPlayer: Client {clientId} doesn't own any mask!");
        return null;
    }

    /// <summary>
    /// Get the client ID who owns a specific mask.
    /// Returns null if mask is not owned.
    /// </summary>
    public ulong? GetOwnerOfMask(MaskType mask)
    {
        if (maskOwners.TryGetValue(mask, out ulong ownerId))
            return ownerId;

        Debug.LogWarning($"[MaskAuthority] GetOwnerOfMask: {mask} has no owner!");
        return null;
    }

    /// <summary>
    /// Server validates and forwards a swap request to the target player.
    /// </summary>
    public void SendRequest(ulong requesterClientId, MaskType requestedMask)
    {
        if (!IsServer)
        {
            Debug.LogError("[MaskAuthority] SendRequest called on client!");
            return;
        }

        // Validation 1: Requester owns a mask
        MaskType? requesterMask = GetMaskOfPlayer(requesterClientId);
        if (requesterMask == null)
        {
            Debug.LogError($"[MaskAuthority] SendRequest failed: Requester {requesterClientId} doesn't own any mask!");
            return;
        }

        // Validation 2: Can't request own mask
        if (requesterMask == requestedMask)
        {
            Debug.LogWarning($"[MaskAuthority] Client {requesterClientId} tried to request their own mask ({requestedMask})");
            return;
        }

        // Validation 3: Requested mask must be owned by someone
        ulong? targetClientId = GetOwnerOfMask(requestedMask);
        if (targetClientId == null)
        {
            Debug.LogError($"[MaskAuthority] SendRequest failed: {requestedMask} has no owner!");
            return;
        }

        // Validation 4: Target client must be connected
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId.Value))
        {
            Debug.LogError($"[MaskAuthority] SendRequest failed: Target client {targetClientId.Value} is not connected!");
            return;
        }

        // Validation 5: Target must have a player object
        var targetClient = NetworkManager.Singleton.ConnectedClients[targetClientId.Value];
        if (targetClient.PlayerObject == null)
        {
            Debug.LogError($"[MaskAuthority] SendRequest failed: Target client {targetClientId.Value} has no PlayerObject!");
            return;
        }

        // All validations passed - send request to target
        PlayerMaskManager targetManager = targetClient.PlayerObject.GetComponent<PlayerMaskManager>();
        if (targetManager == null)
        {
            Debug.LogError($"[MaskAuthority] SendRequest failed: Target PlayerObject has no PlayerMaskManager!");
            return;
        }

        Debug.Log($"[MaskAuthority] Forwarding swap request: Client {requesterClientId} ({requesterMask}) -> Client {targetClientId} ({requestedMask})");
        targetManager.ReceiveMaskRequestClientRpc(requesterClientId, requestedMask);
    }

    /// <summary>
    /// Execute a server-authoritative mask swap between two players.
    /// </summary>
    public void SwapMasks(ulong requesterClientId, ulong targetClientId)
    {
        if (!IsServer)
        {
            Debug.LogError("[MaskAuthority] SwapMasks called on client!");
            return;
        }

        // Validation 1: Both clients exist
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(requesterClientId))
        {
            Debug.LogError($"[MaskAuthority] SwapMasks failed: Requester {requesterClientId} not connected!");
            return;
        }

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(targetClientId))
        {
            Debug.LogError($"[MaskAuthority] SwapMasks failed: Target {targetClientId} not connected!");
            return;
        }

        // Validation 2: Get current masks
        MaskType? requesterMask = GetMaskOfPlayer(requesterClientId);
        MaskType? targetMask = GetMaskOfPlayer(targetClientId);

        if (requesterMask == null)
        {
            Debug.LogError($"[MaskAuthority] SwapMasks failed: Requester {requesterClientId} has no mask!");
            return;
        }

        if (targetMask == null)
        {
            Debug.LogError($"[MaskAuthority] SwapMasks failed: Target {targetClientId} has no mask!");
            return;
        }

        // Validation 3: Get PlayerObjects
        var requesterPlayerObject = NetworkManager.Singleton.ConnectedClients[requesterClientId].PlayerObject;
        var targetPlayerObject = NetworkManager.Singleton.ConnectedClients[targetClientId].PlayerObject;

        if (requesterPlayerObject == null || targetPlayerObject == null)
        {
            Debug.LogError("[MaskAuthority] SwapMasks failed: Missing PlayerObject!");
            return;
        }

        var requesterManager = requesterPlayerObject.GetComponent<PlayerMaskManager>();
        var targetManager = targetPlayerObject.GetComponent<PlayerMaskManager>();

        if (requesterManager == null || targetManager == null)
        {
            Debug.LogError("[MaskAuthority] SwapMasks failed: Missing PlayerMaskManager!");
            return;
        }

        // Perform atomic swap in ownership dictionary
        maskOwners[requesterMask.Value] = targetClientId;
        maskOwners[targetMask.Value] = requesterClientId;

        Debug.Log($"[MaskAuthority] SWAP COMPLETE: Client {requesterClientId} ({requesterMask} -> {targetMask}) <-> Client {targetClientId} ({targetMask} -> {requesterMask})");

        // Update NetworkVariables directly (auto-syncs to all clients)
        requesterManager.SetMask(targetMask.Value);
        targetManager.SetMask(requesterMask.Value);
        
        Debug.Log($"[MaskAuthority] Both players' masks updated via NetworkVariable");
    }

    /// <summary>
    /// Debug: Print current mask ownership state
    /// </summary>
    [ContextMenu("Debug Print Mask Ownership")]
    public void DebugPrintOwnership()
    {
        Debug.Log("=== MASK OWNERSHIP ===");
        foreach (var kvp in maskOwners)
        {
            Debug.Log($"{kvp.Key} -> Client {kvp.Value}");
        }
        Debug.Log("======================");
    }
}

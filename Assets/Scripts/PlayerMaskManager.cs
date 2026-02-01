using UnityEngine;
using Unity.Netcode;
using MaskBound.Player;

/// <summary>
/// Manages mask ownership and swapping for a single player.
/// Server-authoritative with NetworkVariable synchronization.
/// </summary>
public class PlayerMaskManager : NetworkBehaviour
{
    // TODO: Remove this singleton pattern - use event system instead
    public static PlayerMaskManager Local;

    private const float SWAP_COOLDOWN_DURATION = 5f;

    // Server-authoritative mask ownership
    private NetworkVariable<MaskType> currentMask = new NetworkVariable<MaskType>(
        MaskType.Fire, // Default value
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    /// <summary>
    /// Current mask owned by this player. Read-only access.
    /// </summary>
    public MaskType CurrentMask => currentMask.Value;

    private ulong? pendingRequester = null;
    private bool hasMask = false;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            Local = this;

        // Subscribe to mask changes for HUD updates
        currentMask.OnValueChanged += HandleMaskChanged;
        
        // Force initial UI update
        if (IsOwner && hasMask)
        {
            UpdateHUD(currentMask.Value);
        }

        // Server assigns initial masks
        if (IsServer)
        {
            if (MaskAuthority.Instance == null)
            {
                Debug.LogError("[PlayerMaskManager] MaskAuthority.Instance is null! Cannot assign mask.");
                return;
            }

            MaskType assignedMask = MaskAuthority.Instance.AssignInitialMask(OwnerClientId);
            currentMask.Value = assignedMask; // NetworkVariable auto-syncs to all clients
            hasMask = true;

            Debug.Log($"[PlayerMaskManager] Server assigned {assignedMask} to client {OwnerClientId}");
        }
    }

    public override void OnNetworkDespawn()
    {
        currentMask.OnValueChanged -= HandleMaskChanged;
    }

    // ===== REQUEST =====

    /// <summary>
    /// Local player requests to swap for a different mask.
    /// Client-side call that sends ServerRpc.
    /// </summary>
    public void RequestMask(MaskType requestedMask)
    {
        if (!IsOwner)
        {
            Debug.LogWarning("[PlayerMaskManager] RequestMask called on non-owner!");
            return;
        }

        // Validation: Can't request own mask
        if (currentMask.Value == requestedMask)
        {
            Debug.LogWarning($"[PlayerMaskManager] You already have {requestedMask}!");
            return;
        }

        if (!hasMask)
        {
            Debug.LogWarning("[PlayerMaskManager] Cannot request mask - you don't have one yet!");
            return;
        }

        Debug.Log($"[PlayerMaskManager] Requesting swap: {currentMask.Value} -> {requestedMask}");
        RequestMaskServerRpc(requestedMask);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestMaskServerRpc(MaskType requestedMask, RpcParams rpcParams = default)
    {
        if (MaskAuthority.Instance == null)
        {
            Debug.LogError("[PlayerMaskManager] MaskAuthority.Instance is null!");
            return;
        }

        // Use RpcParams to get sender identity
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[PlayerMaskManager] Server received request from client {senderClientId} for {requestedMask}");
        MaskAuthority.Instance.SendRequest(senderClientId, requestedMask);
    }

    // ===== RECEIVE =====

    /// <summary>
    /// Client receives a swap request from another player.
    /// Only the target player (owner of the requested mask) sees this.
    /// </summary>
    [Rpc(SendTo.Owner)]
    public void ReceiveMaskRequestClientRpc(ulong requesterClientId, MaskType requestedMask)
    {
        if(IsOwner)
            AudioManager.instance.PlayOneShot(FMODEvents.instance.requestReceive, transform.position);
        
        Debug.Log($"[PlayerMaskManager] Received swap request from client {requesterClientId} for {requestedMask}");
        
        // Validation: Still own the requested mask?
        if (currentMask.Value != requestedMask)
        {
            Debug.LogWarning($"[PlayerMaskManager] Received request for {requestedMask} but I have {currentMask.Value}. Ignoring.");
            return;
        }

        pendingRequester = requesterClientId;
        
        if (MaskRequestUI.Instance != null)
        {
            MaskRequestUI.Instance.Show();
        }
        else
        {
            Debug.LogError("[PlayerMaskManager] MaskRequestUI.Instance is null!");
        }
    }

    /// <summary>
    /// Local player accepts the pending swap request.
    /// </summary>
    public void AcceptRequest()
    {
        if (!IsOwner)
        {
            Debug.LogWarning("[PlayerMaskManager] AcceptRequest called on non-owner!");
            return;
        }

        if (pendingRequester == null)
        {
            Debug.LogWarning("[PlayerMaskManager] No pending request to accept!");
            return;
        }

        Debug.Log($"[PlayerMaskManager] Accepting swap request from client {pendingRequester.Value}");
        AcceptRequestServerRpc(pendingRequester.Value);
        
        // Clear pending request
        pendingRequester = null;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void AcceptRequestServerRpc(ulong requesterClientId, RpcParams rpcParams = default)
    {
        if (MaskAuthority.Instance == null)
        {
            Debug.LogError("[PlayerMaskManager] MaskAuthority.Instance is null!");
            return;
        }

        // Use RpcParams to get sender identity (the player accepting)
        ulong targetClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[PlayerMaskManager] Server executing swap: Requester={requesterClientId}, Target={targetClientId}");
        MaskAuthority.Instance.SwapMasks(requesterClientId, targetClientId);
    }

    // ===== CALLBACKS =====

    /// <summary>
    /// Called when mask NetworkVariable changes.
    /// Updates HUD for owner only.
    /// Resets corruption timer per GDD: "Mask timer resets when switching masks"
    /// </summary>
    private void HandleMaskChanged(MaskType oldMask, MaskType newMask)
    {
        Debug.Log($"[PlayerMaskManager] Mask changed: {oldMask} -> {newMask} (Owner: {IsOwner})");
        
        if (IsOwner)
        {
            UpdateHUD(newMask);

            AudioManager.instance.PlayOneShot(FMODEvents.instance.maskSwap, transform.position);
            
            // GDD: Reset corruption timer on mask swap
            MaskCorruption corruption = GetComponent<MaskCorruption>();
            if (corruption != null)
            {
                corruption.ResetCorruption();
                Debug.Log("[PlayerMaskManager] Corruption timer reset due to mask swap");
            }
            else
            {
                Debug.LogWarning("[PlayerMaskManager] MaskCorruption component not found!");
            }
        }
    }

    /// <summary>
    /// Updates the HUD to display the current mask.
    /// </summary>
    private void UpdateHUD(MaskType mask)
    {
        if (HUDMaskDisplay.Instance != null)
        {
            HUDMaskDisplay.Instance.Refresh(mask);
            Debug.Log($"[PlayerMaskManager] Updated HUD to show {mask}");
        }
        else
        {
            Debug.LogWarning("[PlayerMaskManager] HUDMaskDisplay.Instance is null!");
        }
    }

    // ===== PUBLIC API =====

    /// <summary>
    /// Returns whether this player currently has a mask assigned.
    /// </summary>
    public bool HasMask()
    {
        return hasMask;
    }

    /// <summary>
    /// Server-only: Directly set this player's mask.
    /// Used by MaskAuthority during swaps.
    /// </summary>
    /// <param name="newMask">The mask to assign</param>
    public void SetMask(MaskType newMask)
    {
        if (!IsServer)
        {
            Debug.LogError("[PlayerMaskManager] SetMask can only be called on server!");
            return;
        }

        currentMask.Value = newMask;
        hasMask = true;
        Debug.Log($"[PlayerMaskManager] Server set mask to {newMask} for client {OwnerClientId}");
    }
}

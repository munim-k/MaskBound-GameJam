using UnityEngine;
using Unity.Netcode;

public class PlayerMaskManager : NetworkBehaviour
{
    public static PlayerMaskManager Local;

    [SerializeField] private MaskType currentMask;
    public MaskType CurrentMask => currentMask;

    private ulong? pendingRequester = null;  // ✅ FIXED: Use nullable to allow Client 0
    private bool hasMask = false;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            Local = this;

        // Server assigns masks
        if (IsServer)
        {
            if (MaskAuthority.Instance == null)
            {
                Debug.LogError("[PlayerMaskManager] MaskAuthority.Instance is null! Cannot assign mask.");
                return;
            }

            MaskType assignedMask = MaskAuthority.Instance.AssignInitialMask(OwnerClientId);
            currentMask = assignedMask;
            hasMask = true;

            Debug.Log($"[PlayerMaskManager] Server assigned {assignedMask} to client {OwnerClientId}");
            
            // Notify all clients (including this one) of the initial mask
            SetMaskClientRpc(assignedMask);
        }
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
        if (currentMask == requestedMask)
        {
            Debug.LogWarning($"[PlayerMaskManager] You already have {requestedMask}!");
            return;
        }

        if (!hasMask)
        {
            Debug.LogWarning("[PlayerMaskManager] Cannot request mask - you don't have one yet!");
            return;
        }

        Debug.Log($"[PlayerMaskManager] Requesting swap: {currentMask} -> {requestedMask}");
        RequestMaskServerRpc(requestedMask);
    }

    [Rpc(SendTo.Server)]
    private void RequestMaskServerRpc(MaskType requestedMask, RpcParams rpcParams = default)
    {
        if (MaskAuthority.Instance == null)
        {
            Debug.LogError("[PlayerMaskManager] MaskAuthority.Instance is null!");
            return;
        }

        Debug.Log($"[PlayerMaskManager] Server received request from client {rpcParams.Receive.SenderClientId} for {requestedMask}");
        MaskAuthority.Instance.SendRequest(rpcParams.Receive.SenderClientId, requestedMask);
    }

    // ===== RECEIVE =====

    /// <summary>
    /// Client receives a swap request from another player.
    /// Only the target player (owner of the requested mask) sees this.
    /// </summary>
    [Rpc(SendTo.Owner)]
    public void ReceiveMaskRequestClientRpc(ulong requesterClientId, MaskType requestedMask)
    {
        Debug.Log($"[PlayerMaskManager] >>>>>> ReceiveMaskRequestClientRpc ENTRY <<<<<<");
        Debug.Log($"[PlayerMaskManager] Received swap request from client {requesterClientId} for {requestedMask}");
        Debug.Log($"[PlayerMaskManager] Current state: currentMask={currentMask}, pendingRequester (BEFORE)={pendingRequester}, IsOwner={IsOwner}");
        
        // Validation: Still own the requested mask?
        if (currentMask != requestedMask)
        {
            Debug.LogWarning($"[PlayerMaskManager] Received request for {requestedMask} but I have {currentMask}. Ignoring.");
            return;
        }

        Debug.Log($"[PlayerMaskManager] Setting pendingRequester from {pendingRequester} to {requesterClientId}");
        pendingRequester = requesterClientId;
        Debug.Log($"[PlayerMaskManager] pendingRequester is now: {pendingRequester}");
        
        if (MaskRequestUI.Instance != null)
        {
            Debug.Log($"[PlayerMaskManager] Calling MaskRequestUI.Instance.Show()");
            MaskRequestUI.Instance.Show();
        }
        else
        {
            Debug.LogError("[PlayerMaskManager] MaskRequestUI.Instance is null!");
        }
        
        Debug.Log($"[PlayerMaskManager] <<<<<< ReceiveMaskRequestClientRpc EXIT <<<<<<");
    }

    /// <summary>
    /// Local player accepts the pending swap request.
    /// </summary>
    public void AcceptRequest()
    {
        Debug.Log($"[PlayerMaskManager] ========== AcceptRequest() CALLED ========== IsOwner={IsOwner}, ClientId={OwnerClientId}");
        
        try
        {
            if (!IsOwner)
            {
                Debug.LogWarning("[PlayerMaskManager] AcceptRequest called on non-owner!");
                return;
            }

            Debug.Log($"[PlayerMaskManager] IsOwner check passed. pendingRequester={pendingRequester}");

            if (pendingRequester == null)  // ✅ FIXED: Check for null instead of 0
            {
                Debug.LogWarning("[PlayerMaskManager] No pending request to accept!");
                return;
            }

            Debug.Log($"[PlayerMaskManager] CLIENT {OwnerClientId}: Accepting swap request from client {pendingRequester.Value}");
            Debug.Log($"[PlayerMaskManager] CLIENT {OwnerClientId}: Calling AcceptRequestServerRpc({pendingRequester.Value})");
            AcceptRequestServerRpc(pendingRequester.Value);  // ✅ FIXED: Use .Value
            
            // Clear pending request
            pendingRequester = null;  // ✅ FIXED: Set to null instead of 0
            Debug.Log($"[PlayerMaskManager] CLIENT {OwnerClientId}: Cleared pending requester");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerMaskManager] EXCEPTION in AcceptRequest: {e.Message}\n{e.StackTrace}");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void AcceptRequestServerRpc(ulong requesterClientId)
    {
        Debug.Log($"[PlayerMaskManager] SERVER: AcceptRequestServerRpc called! Requester={requesterClientId}, Target(me)={OwnerClientId}");
        
        if (MaskAuthority.Instance == null)
        {
            Debug.LogError("[PlayerMaskManager] MaskAuthority.Instance is null!");
            return;
        }

        Debug.Log($"[PlayerMaskManager] SERVER: Calling MaskAuthority.SwapMasks({requesterClientId}, {OwnerClientId})");
        MaskAuthority.Instance.SwapMasks(requesterClientId, OwnerClientId);
        Debug.Log($"[PlayerMaskManager] SERVER: SwapMasks call completed");
    }

    // ===== APPLY =====

    /// <summary>
    /// Server notifies all clients to update this player's mask.
    /// Called after initial assignment or successful swap.
    /// </summary>
    [Rpc(SendTo.Everyone)]
    public void SetMaskClientRpc(MaskType newMask)
    {
        MaskType oldMask = currentMask;
        currentMask = newMask;
        hasMask = true;

        Debug.Log($"[PlayerMaskManager] Client received SetMask: {oldMask} -> {newMask} (IsOwner: {IsOwner}, ClientId: {OwnerClientId})");

        // Update HUD only for local player
        if (IsOwner && HUDMaskDisplay.Instance != null)
        {
            HUDMaskDisplay.Instance.Refresh(newMask);
            Debug.Log($"[PlayerMaskManager] Updated HUD to show {newMask}");
        }
    }

    /// <summary>
    /// Returns whether this player currently has a mask assigned.
    /// </summary>
    public bool HasMask()
    {
        return hasMask;
    }
}

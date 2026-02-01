using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class UIManager : NetworkBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject gameStartPanel;
    [SerializeField] private GameObject uploadPicturePanel;
    [SerializeField] private GameObject characterSelectionPanel;  // Character selection phase
    [SerializeField] private GameObject enterGamePanel;   // Final ready screen

    [Header("Force Start (Host Only)")]
    [SerializeField] private GameObject forceStartButton;
    [SerializeField] private int minPlayers = 2;
    [SerializeField] private int maxPlayers = 3;

    [Header("Enter Game")]
    [SerializeField] private GameObject startGameButton;

    // ❌ OLD meaning: upload confirmed
    // ✅ NEW meaning: face READY (downloaded + spawned)
    private HashSet<ulong> confirmedClients = new HashSet<ulong>();

    private bool uploadPhaseStarted;

    // =========================
    // UNITY
    // =========================

    private void Awake()
    {
        ShowGameStartPanelLocal();


        if (startGameButton != null)
            startGameButton.SetActive(false);
            
        if (characterSelectionPanel != null)
            characterSelectionPanel.SetActive(false);
            
        if (enterGamePanel != null)
            enterGamePanel.SetActive(false);

        if (forceStartButton != null)
        {
            forceStartButton.SetActive(false);
            forceStartButton
                .GetComponent<UnityEngine.UI.Button>()
                .onClick.AddListener(OnForceStartPressed);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
            ShowGameStartPanelLocal();
    }

    private void Update()
    {
        UpdateForceStartVisibility();
    }

    // =========================
    // FORCE START LOGIC (UNCHANGED)
    // =========================

    private void UpdateForceStartVisibility()
    {
        if (forceStartButton == null)
            return;

        if (!IsServer || uploadPhaseStarted)
        {
            forceStartButton.SetActive(false);
            return;
        }

        int connected = NetworkManager.Singleton.ConnectedClients.Count;

        bool shouldShow =
            connected >= minPlayers &&
            connected < maxPlayers;

        forceStartButton.SetActive(shouldShow);
    }

    public void OnForceStartPressed()
    {
        AudioManager.instance.PlayOneShot(FMODEvents.instance.uiClick, Vector3.zero);
        
        Debug.Log("FORCE START BUTTON CLICKED");

        if (!IsServer || uploadPhaseStarted)
            return;

        int connected = NetworkManager.Singleton.ConnectedClients.Count;

        if (connected < minPlayers)
            return;

        StartUploadPhaseServerRpc();
    }

    // =========================
    // UPLOAD PHASE (UNCHANGED)
    // =========================

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StartUploadPhaseServerRpc()
    {
        if (uploadPhaseStarted) return;

        uploadPhaseStarted = true;
        forceStartButton?.SetActive(false);

        ShowUploadPanelClientRpc();
    }

    [ClientRpc]
    private void ShowUploadPanelClientRpc()
    {
        ShowUploadPanelLocal();
    }

    // =========================
    // FACE READY TRACKING (CHANGED)
    // =========================
    // 🔑 CALLED FROM FaceEngine AFTER FACE IS SPAWNED

    public void NotifyFaceReady(ulong clientId)
    {
        if (!IsServer)
            return;

        if (confirmedClients.Contains(clientId))
            return;

        confirmedClients.Add(clientId);

        Debug.Log($"Face ready from client {clientId}");

        if (confirmedClients.Count == NetworkManager.Singleton.ConnectedClients.Count)
        {
            // All faces ready - show character selection instead of enter game
            ShowCharacterSelectionPanelClientRpc();
        }
    }

    [ClientRpc]
    private void ShowCharacterSelectionPanelClientRpc()
    {
        uploadPicturePanel?.SetActive(false);
        characterSelectionPanel?.SetActive(true);
        
        // Initialize clients on server (now that all have uploaded faces and connected)
        if (IsServer)
        {
            var selectionManager = FindObjectOfType<MaskBound.UI.CharacterSelectionManager>();
            selectionManager?.InitializeClients();
        }
        
        // Notify the CharacterSelectionPanel to initialize
        var panel = characterSelectionPanel?.GetComponent<MaskBound.UI.CharacterSelectionPanel>();
        panel?.Show();
        
        Debug.Log("[UIManager] Showing character selection panel");
    }

    /// <summary>
    /// Called by CharacterSelectionManager when all players confirm
    /// </summary>
    public void OnAllCharactersConfirmed()
    {
        ShowEnterGamePanelClientRpc();
    }

    [ClientRpc]
    private void ShowEnterGamePanelClientRpc()
    {
        characterSelectionPanel?.SetActive(false);
        enterGamePanel?.SetActive(true);

        if (startGameButton != null)
            startGameButton.SetActive(IsServer);
            
        Debug.Log("[UIManager] All characters confirmed - showing enter game panel");
    }

    // =========================
    // HOST START GAME (NEW)
    // =========================

    // public void OnStartGamePressed()
    // {
    //     if (!IsServer)
    //         return;

    //     NetworkManager.Singleton.SceneManager.LoadScene(
    //         "Game",
    //         UnityEngine.SceneManagement.LoadSceneMode.Single
    //     );
    // }

    // =========================
    // LOCAL UI HELPERS (UNCHANGED)
    // =========================

    private void ShowGameStartPanelLocal()
    {
        gameStartPanel?.SetActive(true);
        uploadPicturePanel?.SetActive(false);
        characterSelectionPanel?.SetActive(false);
        enterGamePanel?.SetActive(false);
    }

    private void ShowUploadPanelLocal()
    {
        gameStartPanel?.SetActive(false);
        uploadPicturePanel?.SetActive(true);
        characterSelectionPanel?.SetActive(false);
        enterGamePanel?.SetActive(false);
    }
}

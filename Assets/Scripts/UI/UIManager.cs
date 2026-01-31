using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class UIManager : NetworkBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject gameStartPanel;
    [SerializeField] private GameObject uploadPicturePanel;

    [Header("Force Start (Host Only)")]
    [SerializeField] private GameObject forceStartButton;
    [SerializeField] private int minPlayers = 2;
    [SerializeField] private int maxPlayers = 3;

    private HashSet<ulong> confirmedClients = new HashSet<ulong>();
    private bool uploadPhaseStarted;

    // =========================
    // UNITY
    // =========================

    private void Awake()
    {
        ShowGameStartPanelLocal();

        if (forceStartButton != null)
        {
            forceStartButton.SetActive(false);
            forceStartButton.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OnForceStartPressed);
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
    // FORCE START LOGIC
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

    /// <summary>
    /// Called by Host pressing Force Start button
    /// </summary>
    public void OnForceStartPressed()
    {
        Debug.Log("FORCE START BUTTON CLICKED");

        if (!IsServer || uploadPhaseStarted)
            return;

        int connected = NetworkManager.Singleton.ConnectedClients.Count;

        if (connected < minPlayers)
            return;

        StartUploadPhaseServerRpc();
    }

    // =========================
    // UPLOAD PHASE
    // =========================

    [ServerRpc(RequireOwnership = false)]
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
    // CONFIRM TRACKING
    // =========================

    [ServerRpc(RequireOwnership = false)]
    public void ConfirmUploadServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (confirmedClients.Contains(clientId))
            return;

        confirmedClients.Add(clientId);

        if (confirmedClients.Count == NetworkManager.Singleton.ConnectedClients.Count)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(
                "Game",
                UnityEngine.SceneManagement.LoadSceneMode.Single
            );
        }
    }

    // =========================
    // LOCAL UI HELPERS
    // =========================

    private void ShowGameStartPanelLocal()
    {
        gameStartPanel?.SetActive(true);
        uploadPicturePanel?.SetActive(false);
    }

    private void ShowUploadPanelLocal()
    {
        gameStartPanel?.SetActive(false);
        uploadPicturePanel?.SetActive(true);
    }
}

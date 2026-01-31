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
    [SerializeField] private string gameSceneName = "Random";
    [SerializeField] private string timeoutSceneName = "MainMenu";

    [Header("UI")]
    [SerializeField] private GameObject textBox;
    [SerializeField] private GameObject timerText;
    [SerializeField] private GameObject playersJoinedText;
    [SerializeField] private GameObject forceStartButton; // HOST ONLY

    private TextMeshProUGUI timerTMP;
    private TextMeshProUGUI playersTMP;

    private float elapsedTime;
    private bool timerActive;
    private bool gameStarted;
    private bool initialized;

    private void Awake()
    {
        if (timerText != null)
            timerTMP = timerText.GetComponent<TextMeshProUGUI>();

        if (playersJoinedText != null)
            playersTMP = playersJoinedText.GetComponent<TextMeshProUGUI>();

        if (forceStartButton != null)
            forceStartButton.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer || initialized) return;
        initialized = true;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // =========================
    // UI BUTTONS
    // =========================

    public void OnJoinPressed()
    {
        timerActive = true;
        elapsedTime = 0f;

        if (textBox != null) textBox.SetActive(true);
        if (timerText != null) timerText.SetActive(true);
    }

    public void OnForceStartPressed()
    {
        if (!IsServer) return;

        if (GetConnectedPlayers() >= minPlayers)
        {
            Debug.Log("Host forced game start.");
            StartGame();
        }
    }

    public void OnClosePressed()
    {
        timerActive = false;

        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(timeoutSceneName);
    }

    // =========================
    // UPDATE LOOP
    // =========================

    private void Update()
    {
        if (!timerActive || gameStarted) return;

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

        // Host-only force start button
        if (forceStartButton != null)
        {
            bool canForceStart =
                IsServer &&
                connected >= minPlayers &&
                connected < maxPlayers;

            forceStartButton.SetActive(canForceStart);
        }
    }

    // =========================
    // NETWORK EVENTS
    // =========================

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");

        // Optional auto-start when full
        if (GetConnectedPlayers() == maxPlayers)
        {
            StartGame();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");

        if (forceStartButton != null)
            forceStartButton.SetActive(false);
    }

    // =========================
    // MATCH FLOW
    // =========================

    private void StartGame()
    {
        if (gameStarted) return;

        gameStarted = true;
        timerActive = false;

        Debug.Log("Starting co-op game.");

        if (textBox != null) textBox.SetActive(false);
        if (timerText != null) timerText.SetActive(false);
        if (forceStartButton != null) forceStartButton.SetActive(false);

        NetworkManager.Singleton.SceneManager.LoadScene(
            gameSceneName,
            LoadSceneMode.Single
        );
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
    }

    // =========================
    // HELPERS
    // =========================

    private int GetConnectedPlayers()
    {
        return NetworkManager.Singleton != null
            ? NetworkManager.Singleton.ConnectedClients.Count
            : 0;
    }
}

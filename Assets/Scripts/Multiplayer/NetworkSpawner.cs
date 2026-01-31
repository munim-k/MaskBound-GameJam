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
        if (initRan) return;
        initRan = true;

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
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
        Debug.Log($"Client connected: {clientId}");

        // 🔧 Guard added (extra safety)
        if (uploadPhaseStarted)
            return;

        // Auto-start upload ONLY when lobby is full
        if (GetConnectedPlayers() == maxPlayers)
        {
            StartUploadPhase();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
    }

    private void StartUploadPhase()
    {
        if (uploadPhaseStarted) return;
        if (!IsServer) return;

        uploadPhaseStarted = true;
        timerActive = false;

        if (textBox != null) textBox.SetActive(false);
        if (timerText != null) timerText.SetActive(false);

        // 🔑 Delegates UI + flow control to UIManager
        FindObjectOfType<UIManager>()?.StartUploadPhaseServerRpc();
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

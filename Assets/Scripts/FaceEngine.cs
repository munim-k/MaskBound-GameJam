using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Dummiesman;
using SFB;
using UnityEngine.SceneManagement;

public class FaceEngine : NetworkBehaviour
{
    /* =========================
       SETTINGS
       ========================= */

    [Header("MODE")]
    [SerializeField] private bool testMode = false;

    [Header("SPAWN")]
    [SerializeField] private float targetFaceHeight = 0.25f;
    [SerializeField] private float faceDistanceFromCamera = 1.2f;
    
    [Header("GAME SCENE ATTACHMENT")]
    [SerializeField] private Vector3 faceLocalPosition = new Vector3(0, 2f, 0.75f);
    [SerializeField] private float faceLocalScale = 0.001f;

    [Header("TEST MODE")]
    [SerializeField] private GameObject facePrefab;
    [SerializeField] private Texture faceTexture;

    /* =========================
       UI
       ========================= */

    [Header("UI")]
    [SerializeField] private GameObject uploadPanel;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private RawImage previewImage;

    [SerializeField] private Button openPanelButton;
    [SerializeField] private Button closePanelButton;
    [SerializeField] private Button uploadFileButton;
    [SerializeField] private Button takePictureButton;
    [SerializeField] private Button captureButton;
    [SerializeField] private Button confirmButton;

    /* =========================
       BACKEND
       ========================= */

    [Header("Pipeline Server")]
    [SerializeField] private string uploadUrl;

    /* =========================
       RUNTIME
       ========================= */

    private WebCamTexture webCamTexture;
    private Texture2D selectedFaceTexture;

    // ❌ OLD single-face logic (kept, but no longer used)
    private GameObject currentFace;

    // ✅ NEW: one face per player
    private Dictionary<ulong, GameObject> spawnedFaces = new Dictionary<ulong, GameObject>();

    /* =========================
       UNITY
       ========================= */

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        confirmButton.gameObject.SetActive(false);
        captureButton.gameObject.SetActive(false);

        openPanelButton.onClick.AddListener(OpenUploadPanel);
        closePanelButton.onClick.AddListener(CloseUploadPanel);
        uploadFileButton.onClick.AddListener(UploadFromFile);
        takePictureButton.onClick.AddListener(StartCamera);
        captureButton.onClick.AddListener(CapturePhoto);
        confirmButton.onClick.AddListener(ConfirmImage);
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to network scene events (same timing as LocalSpawner)
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        // Wait for LoadEventCompleted (same as LocalSpawner)
        // This ensures players are spawned BEFORE we try to attach faces
        if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
        {
            // Check if we're in the Game scene
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Game")
            {
                Debug.Log("[FaceEngine] LoadEventCompleted in Game scene, attaching faces to players");
                StartCoroutine(AttachFacesToPlayers());
            }
        }
    }

    private IEnumerator AttachFacesToPlayers()
    {
        // Small delay to ensure players are fully ready
        yield return new WaitForSeconds(0.2f);

        Debug.Log($"[FaceEngine] Attaching {spawnedFaces.Count} faces to players");

        foreach (var kvp in spawnedFaces)
        {
            ulong clientId = kvp.Key;
            GameObject face = kvp.Value;
            if (face == null)
            {
                Debug.LogWarning($"[FaceEngine] Face is null for client {clientId}");
                continue;
            }

            // Get the player GameObject for this client
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
            {
                Debug.LogWarning($"[FaceEngine] No client found for {clientId}");
                continue;
            }

            NetworkObject playerNetObj = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
            if (playerNetObj == null)
            {
                Debug.LogError($"[FaceEngine] No PlayerObject for client {clientId} even after LoadEventCompleted!");
                continue;
            }

            AttachFaceToPlayer(face, playerNetObj.gameObject, clientId);
        }
    }



    private void AttachFaceToPlayer(GameObject face, GameObject player, ulong clientId)
    {
        Debug.Log($"[FaceEngine] Attaching face for client {clientId} to player {player.name}");

        // Get the faceLocation component from the player
        faceLocation faceLocationComp = player.GetComponent<faceLocation>();
        if (faceLocationComp == null)
        {
            Debug.LogError($"[FaceEngine] Player {player.name} is missing faceLocation component!");
            return;
        }

        GameObject faceParent = faceLocationComp.FaceParentObject;
        if (faceParent == null)
        {
            Debug.LogError($"[FaceEngine] faceParentObject is null on player {player.name}!");
            return;
        }

        // Parent face to the designated face parent object
        face.transform.SetParent(faceParent.transform);
        
        // Set position and scale - zero offset, 0.001 scale
        face.transform.localPosition = Vector3.zero;
        face.transform.localRotation = Quaternion.identity;
        face.transform.localScale = Vector3.one * 0.001f;
        
        Debug.Log($"[FaceEngine] ✅ Face attached to {faceParent.name}! LocalPos={face.transform.localPosition}, LocalScale={face.transform.localScale}");
    }






    /* =========================
       CONFIRM
       ========================= */

    private void ConfirmImage()
    {
        uploadPanel.SetActive(false);

        if (testMode)
        {
            SpawnFace(NetworkManager.Singleton.LocalClientId,
                      Instantiate(facePrefab),
                      faceTexture);

            return;
        }

        StartCoroutine(SendImageToPipelineServer());
    }

    /* =========================
       PIPELINE SERVER
       ========================= */

    private IEnumerator SendImageToPipelineServer()
    {
        if (selectedFaceTexture == null)
        {
            Debug.LogError("No face texture selected");
            yield break;
        }

        byte[] jpg = selectedFaceTexture.EncodeToJPG();

        string sessionId = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetType().Name;
        string playerId = NetworkManager.Singleton.LocalClientId.ToString();

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", jpg, "face.jpg", "image/jpeg");
        form.AddField("sessionId", sessionId);
        form.AddField("playerId", playerId);

        UnityWebRequest req = UnityWebRequest.Post(uploadUrl, form);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Pipeline upload failed: " + req.error);
            yield break;
        }   

        PipelineResponse response =
            JsonUtility.FromJson<PipelineResponse>(req.downloadHandler.text);

        if (string.IsNullOrEmpty(response.objUrl) ||
            string.IsNullOrEmpty(response.textureUrl))
        {
            Debug.LogError("Invalid pipeline response");
            yield break;
        }

        // 🔑 Send URL + owner info
        SendFaceUrlsServerRpc(response.objUrl, response.textureUrl);
    }

    /* =========================
       NETCODE (URL ONLY)
       ========================= */

    [ServerRpc(RequireOwnership = false)]
    private void SendFaceUrlsServerRpc(string objUrl, string textureUrl, ServerRpcParams rpcParams = default)
    {
        ulong ownerId = rpcParams.Receive.SenderClientId;
        BroadcastFaceUrlsClientRpc(ownerId, objUrl, textureUrl);
    }

    [ClientRpc]
    private void BroadcastFaceUrlsClientRpc(ulong ownerId, string objUrl, string textureUrl)
    {
        StartCoroutine(DownloadAndSpawnFace(ownerId, objUrl, textureUrl));
    }

    /* =========================
       DOWNLOAD + SPAWN
       ========================= */

    private IEnumerator DownloadAndSpawnFace(ulong ownerId, string objUrl, string texUrl)
    {
        UnityWebRequest objReq = UnityWebRequest.Get(objUrl);
        yield return objReq.SendWebRequest();

        if (objReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("OBJ download failed");
            yield break;
        }

        UnityWebRequest texReq = UnityWebRequestTexture.GetTexture(texUrl);
        yield return texReq.SendWebRequest();

        if (texReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Texture download failed");
            yield break;
        }

        byte[] objData = objReq.downloadHandler.data;
        Texture2D tex = DownloadHandlerTexture.GetContent(texReq);

        GameObject face = LoadObjFromBytes(objData);

        SpawnFace(ownerId, face, tex);

        // 🔑 NOW the face is actually ready
        NotifyFaceReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyFaceReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        FindObjectOfType<UIManager>()?.NotifyFaceReady(rpcParams.Receive.SenderClientId);
    }

    /* =========================
       SPAWN LOGIC
       ========================= */

    private void SpawnFace(ulong ownerId, GameObject face, Texture texture)
    {
        if (spawnedFaces.ContainsKey(ownerId))
            Destroy(spawnedFaces[ownerId]);

        Debug.Log($"[FaceEngine] SpawnFace called for client {ownerId}");

        // ✅ CRITICAL: Parent to FaceEngine so face persists through scene changes
        face.transform.SetParent(transform);
        Debug.Log($"[FaceEngine] Face parented to FaceEngine GameObject");

        // Position relative to camera for lobby preview
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 basePos = cam.transform.position + cam.transform.forward * faceDistanceFromCamera;
            face.transform.position = basePos + Vector3.right * (ownerId * 0.6f);
            face.transform.rotation = Quaternion.LookRotation(face.transform.position - cam.transform.position);
            face.transform.Rotate(0f, 180f, 0f);
        }
        else
        {
            Debug.LogWarning("[FaceEngine] No main camera, using default face position");
            face.transform.localPosition = Vector3.zero;
            face.transform.localRotation = Quaternion.identity;
        }

        // Scale face appropriately
        Renderer[] renderers = face.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
                bounds.Encapsulate(r.bounds);

            float scale = targetFaceHeight / bounds.size.y;
            face.transform.localScale = Vector3.one * scale;
            Debug.Log($"[FaceEngine] Face scaled to {scale}");
        }

        spawnedFaces[ownerId] = face;
        Debug.Log($"[FaceEngine] Face stored in spawnedFaces dictionary for client {ownerId}");

        StartCoroutine(ApplyTextureNextFrame(face, texture));
    }

    private IEnumerator ApplyTextureNextFrame(GameObject face, Texture texture)
    {
        yield return null;

        // Try to find URP Lit first (better for faces), then Unlit, then Standard
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Standard");

        if (shader != null)
        {
            foreach (Renderer r in face.GetComponentsInChildren<Renderer>())
            {
                Material m = new Material(shader);
                
                // Assign to both mainTexture (Legacy/Standard) and _BaseMap (URP property name)
                m.mainTexture = texture; 
                if (m.HasProperty("_BaseMap"))
                {
                    m.SetTexture("_BaseMap", texture);
                    m.SetColor("_BaseColor", Color.white); // Ensure color doesn't tint it weirdly
                }

                r.material = m;
            }
        }
        else
        {
            Debug.LogError("[FaceEngine] Could not find any suitable shader to apply face texture!");
        }
    }

    private GameObject LoadObjFromBytes(byte[] objData)
    {
        using (var stream = new MemoryStream(objData))
        {
            OBJLoader loader = new OBJLoader();
            return loader.Load(stream);
        }
    }

    /* =========================
       UI HELPERS
       ========================= */

    private void OpenUploadPanel()
    {
        uploadPanel.SetActive(true);
        mainPanel.SetActive(false);
    }

    private void CloseUploadPanel()
    {
        uploadPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    private void UploadFromFile()
    {
        var paths = StandaloneFileBrowser.OpenFilePanel(
            "Select Face Image",
            "",
            new[] { new ExtensionFilter("Images", "png", "jpg", "jpeg") },
            false
        );

        if (paths.Length > 0)
        {
            selectedFaceTexture = new Texture2D(2, 2);
            selectedFaceTexture.LoadImage(File.ReadAllBytes(paths[0]));
            previewImage.texture = selectedFaceTexture;
            confirmButton.gameObject.SetActive(true);
        }
    }

    private void StartCamera()
    {
        webCamTexture = new WebCamTexture();
        previewImage.texture = webCamTexture;
        webCamTexture.Play();

        captureButton.gameObject.SetActive(true);
    }

    private void CapturePhoto()
    {
        Texture2D photo = new Texture2D(
            webCamTexture.width,
            webCamTexture.height,
            TextureFormat.RGB24,
            false
        );

        photo.SetPixels(webCamTexture.GetPixels());
        photo.Apply();

        webCamTexture.Stop();

        selectedFaceTexture = photo;
        previewImage.texture = photo;
        confirmButton.gameObject.SetActive(true);
    }

    /* =========================
       DATA
       ========================= */

    [System.Serializable]
    private class PipelineResponse
    {
        public string objUrl;
        public string textureUrl;
    }
}

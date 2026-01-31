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
    [SerializeField] private Vector3 faceLocalPosition = new Vector3(0, 0.1f, 0.05f);
    [SerializeField] private Vector3 faceLocalScale = new Vector3(0.3f, 0.3f, 0.3f);
    [SerializeField] private Vector3 fallbackHeadOffset = new Vector3(0, 1.7f, 0.2f);

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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Game")
            return;

        Debug.Log("[FaceEngine] Game scene loaded, attaching faces to players");

        foreach (var kvp in spawnedFaces)
        {
            ulong clientId = kvp.Key;
            GameObject face = kvp.Value;
            if (face == null) continue;

            // Get the player GameObject for this client
            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
            {
                Debug.LogWarning($"[FaceEngine] No client found for {clientId}");
                continue;
            }

            NetworkObject playerNetObj = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
            if (playerNetObj == null)
            {
                Debug.LogWarning($"[FaceEngine] No PlayerObject for client {clientId}, will retry");
                // Player might not be spawned yet, retry after delay
                StartCoroutine(RetryAttachFace(clientId, face));
                continue;
            }

            AttachFaceToPlayer(face, playerNetObj.gameObject, clientId);
        }
    }

    private void AttachFaceToPlayer(GameObject face, GameObject player, ulong clientId)
    {
        Debug.Log($"[FaceEngine] Attaching face for client {clientId} to player {player.name}");

        // Find the head bone (try common names)
        Transform headBone = FindHeadBone(player.transform);

        if (headBone != null)
        {
            Debug.Log($"[FaceEngine] Found head bone: {headBone.name}");
            face.transform.SetParent(headBone);
            face.transform.localPosition = faceLocalPosition;
            face.transform.localRotation = Quaternion.identity;
            face.transform.localScale = faceLocalScale;
        }
        else
        {
            // Fallback: attach to player root with head-level offset
            Debug.LogWarning($"[FaceEngine] No head bone found for player {player.name}, attaching to player root");
            face.transform.SetParent(player.transform);
            face.transform.localPosition = fallbackHeadOffset;
            face.transform.localRotation = Quaternion.identity;
            face.transform.localScale = faceLocalScale;
        }
        
        Debug.Log($"[FaceEngine] ✅ Face attached! LocalPos={face.transform.localPosition}, LocalScale={face.transform.localScale}");
    }

    private Transform FindHeadBone(Transform root)
    {
        // Try common head bone naming conventions
        string[] headNames = { "Head", "head", "Bip001 Head", "mixamorig:Head", "Armature/Head" };

        foreach (string name in headNames)
        {
            // Try direct child first
            Transform found = root.Find(name);
            if (found != null) return found;

            // Try recursive search
            found = FindChildRecursive(root, name);
            if (found != null) return found;
        }

        return null;
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private IEnumerator RetryAttachFace(ulong clientId, GameObject face)
    {
        // Wait for player to spawn (spawning happens after scene load completes)
        yield return new WaitForSeconds(1.0f);

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            Debug.LogError($"[FaceEngine] Client {clientId} disconnected before face attach retry");
            yield break;
        }

        NetworkObject playerNetObj = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (playerNetObj != null)
        {
            AttachFaceToPlayer(face, playerNetObj.gameObject, clientId);
        }
        else
        {
            Debug.LogError($"[FaceEngine] Failed to attach face for client {clientId} - PlayerObject still null after retry");
        }
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

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        foreach (Renderer r in face.GetComponentsInChildren<Renderer>())
        {
            Material m = new Material(shader);
            m.mainTexture = texture;
            r.material = m;
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

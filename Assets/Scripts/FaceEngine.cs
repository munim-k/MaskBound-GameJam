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

        Debug.Log("Game scene loaded – faces carried over");

        foreach (var kvp in spawnedFaces)
        {
            GameObject face = kvp.Value;
            if (face == null) continue;

            // Reset transform for now (temporary placement)
            face.transform.SetParent(null);
            face.transform.position = Vector3.zero;
            face.transform.rotation = Quaternion.identity;
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

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("Main Camera not found");
            return;
        }

        Vector3 basePos = cam.transform.position + cam.transform.forward * faceDistanceFromCamera;

        // Simple spacing per player
        face.transform.position = basePos + Vector3.right * (ownerId * 0.6f);
        face.transform.rotation = Quaternion.LookRotation(face.transform.position - cam.transform.position);
        face.transform.Rotate(0f, 180f, 0f);

        Renderer[] renderers = face.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        float scale = targetFaceHeight / bounds.size.y;
        face.transform.localScale = Vector3.one * scale;

        spawnedFaces[ownerId] = face;

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

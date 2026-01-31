using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using Unity.Netcode;
using System.Collections;
using System.IO;
using Dummiesman;
using SFB;

public class FaceEngine : NetworkBehaviour
{
    [Header("MODE")]
    [SerializeField] private bool testMode = false;

    [SerializeField] private float targetFaceHeight = 0.25f;
    [SerializeField] private float faceDistanceFromCamera = 1.2f;

    [Header("TEST MODE")]
    [SerializeField] private GameObject facePrefab;
    [SerializeField] private Texture faceTexture;

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

    [Header("Backend")]
    [SerializeField] private string uploadUrl;

    private WebCamTexture webCamTexture;
    private Texture2D selectedFaceTexture;
    private GameObject currentFace;

    // =========================
    // UNITY
    // =========================

    private void Awake()
    {
        confirmButton.gameObject.SetActive(false);
        captureButton.gameObject.SetActive(false);

        openPanelButton.onClick.AddListener(OpenUploadPanel);
        closePanelButton.onClick.AddListener(CloseUploadPanel);
        uploadFileButton.onClick.AddListener(UploadFromFile);
        takePictureButton.onClick.AddListener(StartCamera);
        captureButton.onClick.AddListener(CapturePhoto);
        confirmButton.onClick.AddListener(ConfirmImage);
    }

    // =========================
    // CONFIRM
    // =========================

    private void ConfirmImage()
    {
        uploadPanel.SetActive(false);

        if (testMode)
        {
            // Local-only test
            SpawnFace(Instantiate(facePrefab), faceTexture);
            FindObjectOfType<UIManager>()?.ConfirmUploadServerRpc();
            return;
        }

        StartCoroutine(SendImageToPipelineServer());
    }

    // =========================
    // PIPELINE SERVER
    // =========================

    private IEnumerator SendImageToPipelineServer()
    {
        byte[] jpg = selectedFaceTexture.EncodeToJPG();

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", jpg, "face.jpg", "image/jpeg");

        UnityWebRequest req = UnityWebRequest.Post(uploadUrl, form);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(req.error);
            yield break;
        }

        // 🔑 Expect JSON with URLs
        PipelineResponse response =
            JsonUtility.FromJson<PipelineResponse>(req.downloadHandler.text);

        if (string.IsNullOrEmpty(response.objUrl) ||
            string.IsNullOrEmpty(response.textureUrl))
        {
            Debug.LogError("Invalid pipeline response");
            yield break;
        }

        // 🔑 Sync URLs via Netcode
        SendFaceUrlsServerRpc(response.objUrl, response.textureUrl);

        FindObjectOfType<UIManager>()?.ConfirmUploadServerRpc();
    }

    // =========================
    // NETCODE SYNC (URL ONLY)
    // =========================

    [ServerRpc(RequireOwnership = false)]
    private void SendFaceUrlsServerRpc(string objUrl, string textureUrl)
    {
        BroadcastFaceUrlsClientRpc(objUrl, textureUrl);
    }

    [ClientRpc]
    private void BroadcastFaceUrlsClientRpc(string objUrl, string textureUrl)
    {
        StartCoroutine(DownloadAndSpawnFace(objUrl, textureUrl));
    }

    // =========================
    // DOWNLOAD + SPAWN
    // =========================

    private IEnumerator DownloadAndSpawnFace(string objUrl, string texUrl)
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
        SpawnFace(face, tex);
    }

    // =========================
    // SPAWN
    // =========================

    private void SpawnFace(GameObject face, Texture texture)
    {
        if (currentFace != null)
            Destroy(currentFace);

        Camera cam = Camera.main;
        Vector3 pos = cam.transform.position + cam.transform.forward * faceDistanceFromCamera;

        face.transform.position = pos;
        face.transform.rotation = Quaternion.LookRotation(face.transform.position - cam.transform.position);
        face.transform.Rotate(0f, 180f, 0f);

        Renderer[] renderers = face.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        float scale = targetFaceHeight / bounds.size.y;
        face.transform.localScale = Vector3.one * scale;

        currentFace = face;
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

    // =========================
    // FILE + CAMERA
    // =========================

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
        Texture2D photo = new Texture2D(webCamTexture.width, webCamTexture.height);
        photo.SetPixels(webCamTexture.GetPixels());
        photo.Apply();

        webCamTexture.Stop();
        selectedFaceTexture = photo;
        previewImage.texture = photo;
        confirmButton.gameObject.SetActive(true);
    }

    // =========================
    // DATA
    // =========================

    [System.Serializable]
    private class PipelineResponse
    {
        public string objUrl;
        public string textureUrl;
    }
}

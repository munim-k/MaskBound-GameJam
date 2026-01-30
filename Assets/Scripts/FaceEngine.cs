using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using Dummiesman;
using SFB;

public class FaceEngine : MonoBehaviour
{
    /* =======================
       MODE
       ======================= */
    [Header("MODE")]
    [Tooltip("ON = use prefab + texture | OFF = use backend URLs")]
    [SerializeField] private bool testMode = true;

    [SerializeField] private float targetFaceHeight = 0.25f; // world units

    /* =======================
       TEST MODE REFERENCES
       ======================= */
    [Header("TEST MODE (Editor Only)")]
    [SerializeField] private GameObject facePrefab;
    [SerializeField] private Texture faceTexture;

    /* =======================
       SPAWN SETTINGS
       ======================= */
    [Header("Spawn Settings")]
    [SerializeField] private float faceDistanceFromCamera = 1.2f;

    /* =======================
       UI REFERENCES
       ======================= */
    [Header("UI References")]
    [SerializeField] private GameObject uploadPanel;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private RawImage previewImage;

    /* =======================
       BUTTONS
       ======================= */
    [Header("Buttons")]
    [SerializeField] private Button openPanelButton;
    [SerializeField] private Button closePanelButton;
    [SerializeField] private Button uploadFileButton;
    [SerializeField] private Button takePictureButton;
    [SerializeField] private Button captureButton;
    [SerializeField] private Button confirmButton;

    /* =======================
       NETWORKING
       ======================= */
    [Header("Networking")]
    [SerializeField] private string uploadUrl;

    /* =======================
       CAMERA (DEVICE CAMERA)
       ======================= */
    private WebCamTexture webCamTexture;
    private bool cameraRunning = false;

    private Texture2D selectedFaceTexture;
    private GameObject currentFace;

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

    /* =======================
       PANEL CONTROL
       ======================= */
    private void OpenUploadPanel()
    {
        uploadPanel.SetActive(true);
        mainPanel.SetActive(false);
        confirmButton.gameObject.SetActive(false);
    }

    private void CloseUploadPanel()
    {
        StopCamera();
        uploadPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    /* =======================
       FILE UPLOAD
       ======================= */
    private void UploadFromFile()
    {
        StopCamera();

        var extensions = new[]
        {
            new ExtensionFilter("Image Files", "png", "jpg", "jpeg")
        };

        var paths = StandaloneFileBrowser.OpenFilePanel(
            "Select Face Image",
            "",
            extensions,
            false
        );

        if (paths.Length > 0)
        {
            byte[] imageData = File.ReadAllBytes(paths[0]);
            LoadImage(imageData);
        }
    }

    /* =======================
       CAMERA PREVIEW
       ======================= */
    private void StartCamera()
    {
        if (cameraRunning) return;

        webCamTexture = new WebCamTexture();
        previewImage.texture = webCamTexture;
        webCamTexture.Play();

        cameraRunning = true;

        captureButton.gameObject.SetActive(true);
        takePictureButton.gameObject.SetActive(false);
        uploadFileButton.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(false);
    }

    private void CapturePhoto()
    {
        if (!cameraRunning || webCamTexture == null) return;

        Texture2D photo = new Texture2D(
            webCamTexture.width,
            webCamTexture.height,
            TextureFormat.RGB24,
            false
        );

        photo.SetPixels(webCamTexture.GetPixels());
        photo.Apply();

        previewImage.texture = photo;
        selectedFaceTexture = photo;

        StopCamera();

        captureButton.gameObject.SetActive(false);
        takePictureButton.gameObject.SetActive(true);
        uploadFileButton.gameObject.SetActive(true);
        confirmButton.gameObject.SetActive(true);
    }

    private void StopCamera()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            webCamTexture = null;
        }

        cameraRunning = false;
    }

    /* =======================
       LOAD IMAGE
       ======================= */
    private void LoadImage(byte[] imageData)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        texture.LoadImage(imageData);

        previewImage.texture = texture;
        selectedFaceTexture = texture;

        confirmButton.gameObject.SetActive(true);
    }

    /* =======================
       CONFIRM
       ======================= */
    private void ConfirmImage()
    {
        uploadPanel.SetActive(false);

        if (testMode)
        {
            ShowTestFace();
        }
        else
        {
            StartCoroutine(SendImageToServer());
        }
    }

    /* =======================
       TEST MODE
       ======================= */
    private void ShowTestFace()
    {
        SpawnFace(
            Instantiate(facePrefab),
            faceTexture
        );
    }

    /* =======================
       BACKEND MODE
       ======================= */
    private IEnumerator SendImageToServer()
    {
        byte[] jpg = selectedFaceTexture.EncodeToJPG();

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", jpg, "test_image.jpg", "image/jpeg"); // 🔑 MATCH PYTHON

        UnityWebRequest req =
            UnityWebRequest.Post(uploadUrl, form);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(req.error);
            yield break;
        }

        byte[] zipData = req.downloadHandler.data;

        HandleZipResponse(zipData);
    }
    private void HandleZipResponse(byte[] zipData)
    {
        string extractPath =
            Path.Combine(Application.persistentDataPath, "FaceResult");

        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, true);

        Directory.CreateDirectory(extractPath);

        string zipPath = Path.Combine(extractPath, "result.zip");
        File.WriteAllBytes(zipPath, zipData);

        System.IO.Compression.ZipFile.ExtractToDirectory(
            zipPath,
            extractPath
        );

        // 🔍 Find OBJ + texture dynamically
        string objPath = null;
        string texPath = null;

        foreach (string file in Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            string ext = Path.GetExtension(file).ToLower();

            if (ext == ".obj" && objPath == null)
                objPath = file;

            if (fileName == "out.texture.png")
            {
                texPath = file;
            }
            // if ((ext == ".png" || ext == ".jpg" || ext == ".jpeg") && texPath == null)
        }

        if (objPath == null)
        {
            Debug.LogError("No OBJ file found in ZIP");
            return;
        }

        if (texPath == null)
        {
            Debug.LogError("No texture file found in ZIP");
            return;
        }

        Debug.Log("OBJ FOUND: " + objPath);
        Debug.Log("TEXTURE FOUND: " + texPath);

        byte[] objBytes = File.ReadAllBytes(objPath);

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(texPath));

        GameObject runtimeFace = LoadObjFromBytes(objBytes);
        SpawnFace(runtimeFace, tex);
    }


    private IEnumerator DownloadAndDisplayModel(string objUrl, string texUrl)
    {
        UnityWebRequest objReq = UnityWebRequest.Get(objUrl);
        yield return objReq.SendWebRequest();

        UnityWebRequest texReq =
            UnityWebRequestTexture.GetTexture(texUrl);
        yield return texReq.SendWebRequest();

        byte[] objData = objReq.downloadHandler.data;
        Texture2D tex = DownloadHandlerTexture.GetContent(texReq);

        GameObject runtimeFace = LoadObjFromBytes(objData);
        SpawnFace(runtimeFace, tex);
    }

    /* =======================
       SHARED SPAWN LOGIC
       ======================= */
    private void SpawnFace(GameObject face, Texture texture)
    {
        if (face == null)
        {
            Debug.LogError("SpawnFace called with null face");
            return;
        }

        if (currentFace != null)
            Destroy(currentFace);

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("No Main Camera found in scene");
            return;
        }

        // Position in front of camera
        Vector3 spawnPos =
            cam.transform.position + cam.transform.forward * faceDistanceFromCamera;

        face.transform.position = spawnPos;
        face.transform.rotation = Quaternion.LookRotation(
            face.transform.position - cam.transform.position
        );
        face.transform.Rotate(0f, 180f, 0f);

        // 🔑 CALCULATE BOUNDS FROM ALL RENDERERS
        Renderer[] renderers = face.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogError("No Renderers found on loaded face");
            return;
        }

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combinedBounds.Encapsulate(renderers[i].bounds);

        float scaleFactor = targetFaceHeight / combinedBounds.size.y;
        face.transform.localScale = Vector3.one * scaleFactor;

        currentFace = face;

        // 🔑 Apply texture AFTER loader finishes (URP-safe)
        StartCoroutine(ApplyTextureNextFrame(face, texture));
    }

    /* =======================
       OBJ LOADER (BACKEND MODE)
       ======================= */
    private GameObject LoadObjFromBytes(byte[] objData)
    {
        using (var stream = new MemoryStream(objData))
        {
            OBJLoader loader = new OBJLoader();
            GameObject obj = loader.Load(stream);

            obj.name = "RuntimeFace";
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;

            return obj;
        }
    }

    private IEnumerator ApplyTextureNextFrame(GameObject face, Texture texture)
    {
        yield return null;

        Renderer[] renderers = face.GetComponentsInChildren<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            Debug.LogError("URP Unlit shader not found");
            yield break;
        }

        foreach (Renderer r in renderers)
        {
            Material m = new Material(shader);
            m.mainTexture = texture;
            r.material = m;
        }
    }


    [System.Serializable]
    private class ServerResponse
    {
        public string objUrl;
        public string textureUrl;
    }
}

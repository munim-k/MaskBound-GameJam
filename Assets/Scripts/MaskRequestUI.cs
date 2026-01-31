using UnityEngine;
using UnityEngine.UI;

public class MaskRequestUI : MonoBehaviour
{
    public static MaskRequestUI Instance;

    [SerializeField] private GameObject root;

    //Button refrences
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;

    private void Awake()
    {
        Instance = this;
        root.SetActive(false);

        acceptButton.onClick.AddListener(Accept);
        rejectButton.onClick.AddListener(Reject);
    }

    public void Show()
    {
        root.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            Accept();   
        }
        else if (Input.GetKeyDown(KeyCode.N))
        {
            Reject();
        }
    }

    private void Accept()
    {
        Debug.Log("[MaskRequestUI] Accept button clicked!");
        
        if (PlayerMaskManager.Local == null)
        {
            Debug.LogError("[MaskRequestUI] PlayerMaskManager.Local is null!");
            root.SetActive(false);
            return;
        }
        
        root.SetActive(false);
        PlayerMaskManager.Local.AcceptRequest();
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        
        Debug.Log("[MaskRequestUI] Accept completed, UI closed");
    }

    private void Reject()
    {
        Debug.Log("[MaskRequestUI] Reject button clicked!");
        root.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}

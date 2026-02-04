using UnityEngine;
using UnityEngine.UI;

public class MaskSwapPanelUITutorial : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button fireButton;
    [SerializeField] private Button lightningButton;
    [SerializeField] private Button earthButton;

    private void Awake()
    {
        // Wire up button listeners
        if (fireButton != null)
            fireButton.onClick.AddListener(RequestFire);
        
        if (lightningButton != null)
            lightningButton.onClick.AddListener(RequestLightning);
        
        if (earthButton != null)
            earthButton.onClick.AddListener(RequestEarth);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (root.activeSelf)
                Close();
            else
                Open();
        }
    }

    public void Open()
    {
        if (PlayerMaskManager.Local == null)
        {
            Debug.LogWarning("[MaskSwapPanelUI] Cannot open - PlayerMaskManager.Local is null!");
            return;
        }

        root.SetActive(true);
        RefreshButtonStates();
        
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Close()
    {
        root.SetActive(false);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>
    /// Updates button interactivity - disables button for currently owned mask.
    /// </summary>
    private void RefreshButtonStates()
    {
        if (PlayerMaskManager.Local == null)
            return;

        MaskType currentMask = PlayerMaskManager.Local.CurrentMask;

        if (fireButton != null)
            fireButton.interactable = (currentMask != MaskType.Fire);

        if (lightningButton != null)
            lightningButton.interactable = (currentMask != MaskType.Lightning);

        if (earthButton != null)
            earthButton.interactable = (currentMask != MaskType.Earth);
    }

    public void RequestFire()
    {
        if (PlayerMaskManager.Local != null)
        {
            PlayerMaskManager.Local.SetMaskClientRpc(MaskType.Fire);
            Close();
        }
    }

    public void RequestLightning()
    {
        if (PlayerMaskManager.Local != null)
        {
            PlayerMaskManager.Local.SetMaskClientRpc(MaskType.Lightning);
            Close();
        }
    }

    public void RequestEarth()
    {
        if (PlayerMaskManager.Local != null)
        {
            PlayerMaskManager.Local.SetMaskClientRpc(MaskType.Earth);
            Close();
        }
    }
}

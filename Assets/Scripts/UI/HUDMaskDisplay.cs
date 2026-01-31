using UnityEngine;
using UnityEngine.UI;

public class HUDMaskDisplay : MonoBehaviour
{
    public static HUDMaskDisplay Instance;

    [SerializeField] private Image image;
    [SerializeField] private Sprite fireSprite;
    [SerializeField] private Sprite lightningSprite;
    [SerializeField] private Sprite earthSprite;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Updates the HUD to display the specified mask icon.
    /// </summary>
    public void Refresh(MaskType mask)
    {
        if (image == null)
        {
            Debug.LogWarning("[HUDMaskDisplay] Image component is not assigned!");
            return;
        }

        Sprite newSprite = mask switch
        {
            MaskType.Fire => fireSprite,
            MaskType.Lightning => lightningSprite,
            MaskType.Earth => earthSprite,
            _ => null
        };

        if (newSprite != null)
        {
            image.sprite = newSprite;
            Debug.Log($"[HUDMaskDisplay] Updated HUD to show {mask}");
        }
        else
        {
            Debug.LogWarning($"[HUDMaskDisplay] No sprite assigned for {mask}!");
        }
    }
}

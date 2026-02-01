using UnityEngine;
using UnityEngine.UI;

public class GifPlayer : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite[] frames; // Drag all your sprites here
    [SerializeField] private float framesPerSecond = 30f;

    void Update()
    {
        if (frames.Length == 0) return;

        // Use unscaledTime so it keeps spinning even if the game is paused
        int index = (int)(Time.unscaledTime * framesPerSecond);
        
        // The modulo operator (%) loops the index back to 0 when it hits the end
        index = index % frames.Length;

        targetImage.sprite = frames[index];
    }
}
using UnityEngine;
using TMPro; // Standard for Unity Text now. Change to 'using UnityEngine.UI;' if using legacy Text
using System.Collections;

public class LoadingManager : MonoBehaviour
{
    [System.Serializable]
    public class LoadingStep
    {
        [TextArea] public string text;
        public float duration;
    }
    
    [Header("UI References")]
    [SerializeField] private GameObject contentParent;
    [SerializeField] private RectTransform spinner;
    [SerializeField] private TextMeshProUGUI loadingText; // Drag your Text object here

    [Header("Settings")]
    [SerializeField] private float textChangeInterval = 1.5f; // How fast text changes
    [SerializeField] private float spinSpeed = 200f;

    [Header("Sequence")]
    [SerializeField] private LoadingStep[] loadingSequence = new LoadingStep[]
    {
        new LoadingStep { text = "Uploading Image...", duration = 3.0f },
        new LoadingStep { text = "Scanning facial geometry...", duration = 1.5f },
        new LoadingStep { text = "Detecting landmarks...", duration = 1.0f },
        new LoadingStep { text = "Consulting the AI...", duration = 2.0f },
        new LoadingStep { text = "Refining mesh topology...", duration = 1.5f },
        new LoadingStep { text = "Applying textures...", duration = 1.0f },
        new LoadingStep { text = "Calculating polygons...", duration = 1.0f },
        new LoadingStep { text = "Almost there...", duration = 0.5f } // Last one stays until done
    };

    private bool _isLoading = false;
    private Coroutine _textCoroutine;

    void Start()
    {
        Hide();
    }

    public void Show()
    {
        _isLoading = true;
        contentParent.SetActive(true);

        // Start the text cycle
        if (_textCoroutine != null) StopCoroutine(_textCoroutine);
        _textCoroutine = StartCoroutine(CycleTextRoutine());
    }

    public void Hide()
    {
        _isLoading = false;
        contentParent.SetActive(false);

        // Clean up coroutine
        if (_textCoroutine != null) StopCoroutine(_textCoroutine);
    }

    private IEnumerator CycleTextRoutine()
    {
        int index = 0;
        
        while (_isLoading)
        {
            if (loadingSequence.Length > 0)
            {
                // Get the current step
                LoadingStep currentStep = loadingSequence[index];

                // Apply text
                loadingText.text = currentStep.text;

                // Wait for THIS specific step's duration
                yield return new WaitForSeconds(currentStep.duration);

                // Move to next index (stopping at the last one)
                if (index < loadingSequence.Length - 1)
                {
                    index++;
                }
            }
            else
            {
                // Fallback if list is empty
                yield return null;
            }
        }
    }
}
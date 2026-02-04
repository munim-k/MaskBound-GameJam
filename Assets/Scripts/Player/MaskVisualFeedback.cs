using UnityEngine;

namespace MaskBound.Player
{
    /// <summary>
    /// Provides visual feedback for mask corruption
    /// 
    /// GDD: "Mask visually cracks as corruption builds"
    /// 
    /// This component progressively shows crack textures based on corruption progress.
    /// Requires MaskCorruption component on the same GameObject.
    /// </summary>
    public class MaskVisualFeedback : MonoBehaviour
    {
        [Header("Mask Visual Setup")]
        [Tooltip("Material to apply crack textures to (usually the mask material)")]
        [SerializeField] private Material maskMaterial;
        
        [Tooltip("Normal mask texture (0% corruption)")]
        [SerializeField] private Texture normalTexture;
        
        [Header("Crack Progression")]
        [Tooltip("Crack textures in order of corruption severity (0%, 25%, 50%, 75%, 100%)")]
        [SerializeField] private Texture[] crackTextures = new Texture[5];
        
        [Tooltip("Should we also modulate material color as corruption increases?")]
        [SerializeField] private bool modulateColor = true;
        
        [Tooltip("Color tint at maximum corruption")]
        [SerializeField] private Color maxCorruptionColor = new Color(0.5f, 0f, 0.5f, 1f); // Purple tint
        
        private MaskCorruption corruptionComponent;
        private Color originalColor;
        private int lastCrackLevel = -1;

        private void Start()
        {
            corruptionComponent = GetComponent<MaskCorruption>();
            
            if (corruptionComponent == null)
            {
                Debug.LogError("[MaskVisualFeedback] No MaskCorruption component found!");
                enabled = false;
                return;
            }

            if (maskMaterial == null)
            {
                Debug.LogWarning("[MaskVisualFeedback] No mask material assigned!");
                enabled = false;
                return;
            }

            // Store original color
            originalColor = maskMaterial.color;
            
            // Set initial texture
            if (normalTexture != null)
            {
                maskMaterial.mainTexture = normalTexture;
            }
            
            Debug.Log($"[MaskVisualFeedback] Initialized with {crackTextures.Length} crack levels");
        }

        private void Update()
        {
            if (corruptionComponent == null || maskMaterial == null) return;

            float corruptionProgress = corruptionComponent.GetCorruptionProgress();
            
            // Determine crack level (0 to crackTextures.Length - 1)
            int crackLevel = Mathf.FloorToInt(corruptionProgress * (crackTextures.Length - 1));
            crackLevel = Mathf.Clamp(crackLevel, 0, crackTextures.Length - 1);

            // Only update if crack level changed
            if (crackLevel != lastCrackLevel)
            {
                UpdateCrackTexture(crackLevel, corruptionProgress);
                lastCrackLevel = crackLevel;
            }

            // Gradually tint color if enabled
            if (modulateColor)
            {
                Color currentColor = Color.Lerp(originalColor, maxCorruptionColor, corruptionProgress);
                maskMaterial.color = currentColor;
            }
        }

        /// <summary>
        /// Update the crack texture based on corruption level
        /// </summary>
        private void UpdateCrackTexture(int level, float progress)
        {
            if (level == 0 && normalTexture != null)
            {
                // No corruption - use normal texture
                maskMaterial.mainTexture = normalTexture;
                Debug.Log("[MaskVisualFeedback] Mask normal (0% corruption)");
            }
            else if (level > 0 && level < crackTextures.Length && crackTextures[level] != null)
            {
                // Show crack texture
                maskMaterial.mainTexture = crackTextures[level];
                Debug.Log($"[MaskVisualFeedback] Mask cracking - Level {level}/{crackTextures.Length - 1} ({progress * 100:F0}% corruption)");
            }
            else
            {
                Debug.LogWarning($"[MaskVisualFeedback] Crack texture {level} not assigned!");
            }
        }

        /// <summary>
        /// Reset visual feedback (called when corruption resets)
        /// </summary>
        public void ResetVisuals()
        {
            if (maskMaterial != null)
            {
                if (normalTexture != null)
                {
                    maskMaterial.mainTexture = normalTexture;
                }
                
                maskMaterial.color = originalColor;
                lastCrackLevel = -1;
                
                Debug.Log("[MaskVisualFeedback] Visuals reset to normal");
            }
        }

        private void OnDestroy()
        {
            // Restore original material state
            if (maskMaterial != null)
            {
                maskMaterial.color = originalColor;
                if (normalTexture != null)
                {
                    maskMaterial.mainTexture = normalTexture;
                }
            }
        }
    }
}

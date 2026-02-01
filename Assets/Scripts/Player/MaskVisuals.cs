using UnityEngine;
using Unity.Netcode;
using MaskBound.Core.Enums;

namespace MaskBound.Player
{
    /// <summary>
    /// Manages physical mask model visuals on the player
    /// Spawns and swaps 3D mask models when player changes masks
    /// Similar to FaceEngine but for masks
    /// </summary>
    public class MaskVisuals : NetworkBehaviour
    {
        [Header("Mask Prefabs")]
        [Tooltip("Fire mask 3D model prefab")]
        [SerializeField] private GameObject fireMaskPrefab;
        
        [Tooltip("Lightning mask 3D model prefab")]
        [SerializeField] private GameObject lightningMaskPrefab;
        
        [Tooltip("Earth mask 3D model prefab")]
        [SerializeField] private GameObject earthMaskPrefab;

        [Header("Mask Positioning")]
        [Tooltip("Local position offset for mask (relative to player)")]
        [SerializeField] private Vector3 maskLocalPosition = new Vector3(0f, 2f, 0.75f);
        
        [Tooltip("Local rotation for mask")]
        [SerializeField] private Vector3 maskLocalRotation = Vector3.zero;
        
        [Tooltip("Local scale for mask")]
        [SerializeField] private float maskLocalScale = 0.001f;

        // Reference to currently active mask GameObject
        private GameObject currentMaskInstance;
        private PlayerMaskManager maskManager;

        public override void OnNetworkSpawn()
        {
            maskManager = GetComponent<PlayerMaskManager>();
            
            if (maskManager == null)
            {
                Debug.LogError("[MaskVisuals] No PlayerMaskManager found on this GameObject!");
                return;
            }

            // Subscribe to mask changes - need to access the private NetworkVariable via reflection or add a public method
            // For now, we'll poll in Update() - see Update() method below
            
            // Spawn initial mask if player already has one
            if (maskManager.CurrentMask != 0)
            {
                SpawnMask(maskManager.CurrentMask);
            }

            Debug.Log($"[MaskVisuals] Initialized for Client {OwnerClientId}");
        }

        private MaskType lastKnownMask = 0;

        private void Update()
        {
            // Poll for mask changes since we can't subscribe to private NetworkVariable
            if (maskManager != null && maskManager.CurrentMask != lastKnownMask)
            {
                OnMaskChanged(lastKnownMask, maskManager.CurrentMask);
                lastKnownMask = maskManager.CurrentMask;
            }
        }

        public override void OnNetworkDespawn()
        {
            // No need to unsubscribe since we're polling
        }

        /// <summary>
        /// Called when player's mask NetworkVariable changes
        /// </summary>
        private void OnMaskChanged(MaskType oldMask, MaskType newMask)
        {
            Debug.Log($"[MaskVisuals] Mask changed: {oldMask} -> {newMask}");

            // Destroy old mask if it exists
            if (currentMaskInstance != null)
            {
                Destroy(currentMaskInstance);
                currentMaskInstance = null;
                Debug.Log($"[MaskVisuals] Destroyed old mask: {oldMask}");
            }

            // Spawn new mask (all types are valid - Fire=1, Lightning=2, Earth=3)
            if (newMask != 0)
            {
                SpawnMask(newMask);
            }
        }

        /// <summary>
        /// Spawn a mask model and attach it to the player
        /// </summary>
        private void SpawnMask(MaskType maskType)
        {
            GameObject maskPrefab = GetMaskPrefab(maskType);

            if (maskPrefab == null)
            {
                Debug.LogError($"[MaskVisuals] No prefab assigned for {maskType} mask!");
                return;
            }

            // Instantiate mask
            currentMaskInstance = Instantiate(maskPrefab);
            
            // Parent to player
            currentMaskInstance.transform.SetParent(transform);
            
            // Set local transform
            currentMaskInstance.transform.localPosition = maskLocalPosition;
            currentMaskInstance.transform.localRotation = Quaternion.Euler(maskLocalRotation);
            currentMaskInstance.transform.localScale = Vector3.one * maskLocalScale;

            Debug.Log($"[MaskVisuals] Spawned {maskType} mask at position {maskLocalPosition}");
        }

        /// <summary>
        /// Get the prefab for a specific mask type
        /// </summary>
        private GameObject GetMaskPrefab(MaskType maskType)
        {
            return maskType switch
            {
                MaskType.Fire => fireMaskPrefab,
                MaskType.Lightning => lightningMaskPrefab,
                MaskType.Earth => earthMaskPrefab,
                _ => null
            };
        }

        /// <summary>
        /// Get the current mask instance (for external systems like MaskVisualFeedback)
        /// </summary>
        public GameObject GetCurrentMaskInstance()
        {
            return currentMaskInstance;
        }

        /// <summary>
        /// Force refresh the mask visual (useful if prefabs change at runtime)
        /// </summary>
        public void RefreshMask()
        {
            if (maskManager != null && maskManager.CurrentMask != 0)
            {
                OnMaskChanged(maskManager.CurrentMask, maskManager.CurrentMask);
            }
        }

        // Debug visualization
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                // Show where mask will spawn in editor
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position + maskLocalPosition, 0.2f);
            }
        }
    }
}

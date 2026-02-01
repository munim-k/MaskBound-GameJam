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

            // Subscribe to mask changes via public event
            maskManager.OnMaskChanged += OnMaskChanged;
            
            // Spawn initial mask if player already has one
            if (maskManager.CurrentMask != 0)
            {
                Debug.Log($"[MaskVisuals] Initial mask: {maskManager.CurrentMask} for Client {OwnerClientId}");
                SpawnMask(maskManager.CurrentMask);
            }

            Debug.Log($"[MaskVisuals] ✅ Subscribed to mask changes for Client {OwnerClientId}");
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe from event
            if (maskManager != null)
            {
                maskManager.OnMaskChanged -= OnMaskChanged;
            }
        }

        /// <summary>
        /// Called when player's mask NetworkVariable changes
        /// </summary>
        private void OnMaskChanged(MaskType oldMask, MaskType newMask)
        {
            Debug.Log($"[MaskVisuals] 🎭 EVENT RECEIVED: Mask changed {oldMask} -> {newMask} for Client {OwnerClientId}");

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
        /// Spawn a mask model and attach it to the player's mask parent object
        /// </summary>
        private void SpawnMask(MaskType maskType)
        {
            GameObject maskPrefab = GetMaskPrefab(maskType);

            if (maskPrefab == null)
            {
                Debug.LogError($"[MaskVisuals] No prefab assigned for {maskType} mask!");
                return;
            }

            // Get the faceLocation component from the player
            faceLocation faceLocationComp = GetComponent<faceLocation>();
            if (faceLocationComp == null)
            {
                Debug.LogError($"[MaskVisuals] Player {gameObject.name} is missing faceLocation component!");
                return;
            }

            GameObject maskParent = faceLocationComp.MaskParentObject;
            if (maskParent == null)
            {
                Debug.LogError($"[MaskVisuals] maskParentObject is null on player {gameObject.name}!");
                return;
            }

            // Instantiate mask
            currentMaskInstance = Instantiate(maskPrefab);
            
            // Parent to designated mask parent object
            currentMaskInstance.transform.SetParent(maskParent.transform);
            
            // Set local transform - zero offset, no scale change
            currentMaskInstance.transform.localPosition = Vector3.zero;
            currentMaskInstance.transform.localRotation = Quaternion.identity;
            currentMaskInstance.transform.localScale = Vector3.one;

            Debug.Log($"[MaskVisuals] ✅ Spawned {maskType} mask in {maskParent.name} with zero offset and original scale");
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

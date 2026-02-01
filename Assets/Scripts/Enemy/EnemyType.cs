using UnityEngine;
using MaskBound.Core.Enums;

namespace MaskBound.Enemy
{
    /// <summary>
    /// Defines enemy's family and element for GDD damage calculation.
    /// 
    /// GDD System:
    /// - Enemy Family (type): Countered by player affinity for 2x damage
    /// - Enemy Element (variant): Countered by player mask for 2x damage
    /// - Perfect counter: Both match = 4x damage
    /// 
    /// Examples:
    /// - Ice Orc: Family=Orc, Element=Ice
    /// - Metal Scorpion Man: Family=ScorpionMan, Element=Metal
    /// - Rock Gargoyle: Family=Gargoyle, Element=Rock
    /// </summary>
    public class EnemyClassification : MonoBehaviour
    {
        [Header("Enemy Classification (GDD)")]
        [Tooltip("Enemy family - countered by player AFFINITY (permanent)\nOrc | ScorpionMan | Gargoyle")]
        [SerializeField] private EnemyFamily family = EnemyFamily.Orc;
        
        [Tooltip("Enemy element - countered by player MASK (swappable)\nIce | Metal | Rock")]
        [SerializeField] private EnemyElement element = EnemyElement.Ice;

        /// <summary>
        /// Enemy family type - countered by player affinity
        /// </summary>
        public EnemyFamily Family => family;

        /// <summary>
        /// Enemy elemental variant - countered by player mask
        /// </summary>
        public EnemyElement Element => element;

        private void OnValidate()
        {
            // Display current enemy classification in inspector
            gameObject.name = GetEnemyDisplayName();
        }

        /// <summary>
        /// Get a readable enemy name based on element and family
        /// </summary>
        private string GetEnemyDisplayName()
        {
            string baseName = gameObject.name;
            
            // Remove old element/family prefixes if they exist
            baseName = baseName.Replace("Ice ", "").Replace("Metal ", "").Replace("Rock ", "");
            baseName = baseName.Replace("Troll", "").Replace("ScorpionMan", "").Replace("Gargoyle", "").Trim();
            
            // Build new name: "[Element] [Family] [BaseName]"
            string familyName = family switch
            {
                EnemyFamily.Orc => "Orc",
                EnemyFamily.ScorpionMan => "ScorpionMan",
                EnemyFamily.Gargoyle => "Gargoyle",
                _ => "Enemy"
            };

            string elementName = element switch
            {
                EnemyElement.Ice => "Ice",
                EnemyElement.Metal => "Metal",
                EnemyElement.Rock => "Rock",
                _ => ""
            };

            return string.IsNullOrEmpty(baseName) 
                ? $"{elementName} {familyName}" 
                : $"{elementName} {familyName} ({baseName})";
        }

        /// <summary>
        /// Get weakness description for UI/debug
        /// </summary>
        public string GetWeaknessInfo()
        {
            string affinityWeak = family switch
            {
                EnemyFamily.Orc => "Orc affinity",
                EnemyFamily.ScorpionMan => "ScorpionMan affinity",
                EnemyFamily.Gargoyle => "Gargoyle affinity",
                _ => "Unknown"
            };

            string maskWeak = element switch
            {
                EnemyElement.Ice => "Fire mask",
                EnemyElement.Metal => "Lightning mask",
                EnemyElement.Rock => "Earth mask",
                _ => "Unknown"
            };

            return $"Weak to: {affinityWeak} (2x) + {maskWeak} (2x) = 4x total";
        }

        // Debug visualization
        private void OnDrawGizmosSelected()
        {
            // Show element color
            Gizmos.color = element switch
            {
                EnemyElement.Ice => Color.cyan,
                EnemyElement.Metal => Color.gray,
                EnemyElement.Rock => new Color(0.6f, 0.4f, 0.2f), // Brown
                _ => Color.white
            };
            
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 0.5f);
        }
    }
}

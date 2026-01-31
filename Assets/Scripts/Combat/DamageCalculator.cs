using UnityEngine;
using Unity.Netcode;
using MaskBound.Core.Data;
using MaskBound.Core.Enums;

namespace MaskBound.Combat
{
    /// <summary>
    /// Utility class for calculating damage based on GDD affinity/element rules.
    /// 
    /// Damage Formula:
    /// - Base damage from weapon
    /// - 2x multiplier if mask affinity counters enemy family
    /// - 2x multiplier if mask element counters enemy element  
    /// - Total can be 4x if both apply
    /// </summary>
    public static class DamageCalculator
    {
        // Damage multipliers from GDD
        private const float AFFINITY_MULTIPLIER = 2f;
        private const float ELEMENT_MULTIPLIER = 2f;
        private const float BASE_MULTIPLIER = 1f;

        /// <summary>
        /// Calculate final damage with all multipliers
        /// </summary>
        public static DamageResult CalculateDamage(
            float baseDamage,
            MaskType attackerMask,
            EnemyFamily targetFamily,
            EnemyElement targetElement)
        {
            float multiplier = BASE_MULTIPLIER;
            bool affinityBonus = false;
            bool elementCounter = false;

            // Check affinity bonus (mask vs family)
            if (CheckAffinityCounter(attackerMask, targetFamily))
            {
                multiplier *= AFFINITY_MULTIPLIER;
                affinityBonus = true;
            }

            // Check element counter (mask element vs enemy element)
            if (CheckElementCounter(attackerMask, targetElement))
            {
                multiplier *= ELEMENT_MULTIPLIER;
                elementCounter = true;
            }

            float finalDamage = baseDamage * multiplier;

            return new DamageResult
            {
                BaseDamage = baseDamage,
                FinalDamage = finalDamage,
                Multiplier = multiplier,
                AffinityBonus = affinityBonus,
                ElementCounter = elementCounter,
                WasLethal = false // Set by caller after health check
            };
        }

        /// <summary>
        /// Check if mask affinity counters enemy family
        /// Fire -> Trolls, Lightning -> Scorpion Men, Earth -> Gargoyles
        /// </summary>
        private static bool CheckAffinityCounter(MaskType mask, EnemyFamily family)
        {
            return (mask, family) switch
            {
                (MaskType.Fire, EnemyFamily.Troll) => true,
                (MaskType.Lightning, EnemyFamily.ScorpionMan) => true,
                (MaskType.Earth, EnemyFamily.Gargoyle) => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if mask element counters enemy element
        /// Fire -> Lightning, Lightning -> Earth, Earth -> Fire (rock-paper-scissors)
        /// </summary>
        private static bool CheckElementCounter(MaskType mask, EnemyElement element)
        {
            return (mask, element) switch
            {
                (MaskType.Fire, EnemyElement.Lightning) => true,
                (MaskType.Lightning, EnemyElement.Earth) => true,
                (MaskType.Earth, EnemyElement.Fire) => true,
                _ => false
            };
        }

        /// <summary>
        /// Get recommended mask for maximum damage against enemy
        /// </summary>
        public static MaskType GetOptimalMask(EnemyFamily family, EnemyElement element)
        {
            // Priority 1: Affinity counter (2x)
            MaskType affinityMask = family switch
            {
                EnemyFamily.Troll => MaskType.Fire,
                EnemyFamily.ScorpionMan => MaskType.Lightning,
                EnemyFamily.Gargoyle => MaskType.Earth,
                _ => MaskType.Fire
            };

            // Priority 2: Element counter (2x)
            MaskType elementMask = element switch
            {
                EnemyElement.Lightning => MaskType.Fire,
                EnemyElement.Earth => MaskType.Lightning,
                EnemyElement.Fire => MaskType.Earth,
                _ => MaskType.Fire
            };

            // If affinity mask also counters element, use it (4x damage)
            if (CheckElementCounter(affinityMask, element))
                return affinityMask;

            // Otherwise prefer affinity (family counter is more important per GDD)
            return affinityMask;
        }

        /// <summary>
        /// Debug: Print damage calculation breakdown
        /// </summary>
        public static string GetDamageBreakdown(DamageResult result)
        {
            string breakdown = $"Damage: {result.BaseDamage} x {result.Multiplier} = {result.FinalDamage}\n";
            
            if (result.AffinityBonus)
                breakdown += "  + Affinity Bonus (2x)\n";
            
            if (result.ElementCounter)
                breakdown += "  + Element Counter (2x)\n";
            
            if (!result.AffinityBonus && !result.ElementCounter)
                breakdown += "  No bonuses applied\n";

            return breakdown;
        }
    }
}

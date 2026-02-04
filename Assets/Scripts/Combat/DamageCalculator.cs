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
        /// [GDD-COMPLIANT] Calculate final damage with all multipliers
        /// Uses separate PlayerAffinity and MaskType as per GDD specification
        /// </summary>
        /// <param name="baseDamage">Base weapon damage</param>
        /// <param name="playerAffinity">Player's permanent affinity (counters enemy family)</param>
        /// <param name="playerMask">Player's current mask (counters enemy element)</param>
        /// <param name="targetFamily">Enemy family type</param>
        /// <param name="targetElement">Enemy element type</param>
        public static DamageResult CalculateDamage(
            float baseDamage,
            EnemyFamily playerAffinity,
            MaskType playerMask,
            EnemyFamily targetFamily,
            EnemyElement targetElement)
        {
            Debug.Log($"[DamageCalculator] === GDD-COMPLIANT DAMAGE CALC START ===");
            Debug.Log($"[DamageCalculator] Base: {baseDamage}, Affinity: {playerAffinity}, Mask: {playerMask}");
            Debug.Log($"[DamageCalculator] Target: {targetFamily}/{targetElement}");

            float multiplier = BASE_MULTIPLIER;
            bool affinityBonus = false;
            bool elementCounter = false;

            // Check affinity bonus (player affinity vs enemy family)
            if (playerAffinity == targetFamily)
            {
                multiplier *= AFFINITY_MULTIPLIER;
                affinityBonus = true;
                Debug.Log($"[DamageCalculator] ✅ Affinity Bonus: {playerAffinity} counters {targetFamily} ({multiplier}x)");
            }

            // Check element counter (mask vs enemy element)
            if (CheckElementCounter(playerMask, targetElement))
            {
                multiplier *= ELEMENT_MULTIPLIER;
                elementCounter = true;
                Debug.Log($"[DamageCalculator] ✅ Element Counter: {playerMask} counters {targetElement} ({multiplier}x)");
            }

            float finalDamage = baseDamage * multiplier;

            Debug.Log($"[DamageCalculator] === FINAL: {baseDamage} x {multiplier} = {finalDamage} ===");

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
        /// [DEPRECATED] Old damage calculation - uses mask for affinity (WRONG per GDD)
        /// Use the overload with EnemyFamily playerAffinity parameter instead
        /// Kept for backward compatibility during migration
        /// </summary>
        [System.Obsolete("Use CalculateDamage with PlayerAffinity parameter instead")]
        public static DamageResult CalculateDamage(
            float baseDamage,
            MaskType attackerMask,
            EnemyFamily targetFamily,
            EnemyElement targetElement)
        {
            Debug.LogWarning("[DamageCalculator] Using DEPRECATED damage calc (mask-based affinity)!");
            Debug.Log($"[DamageCalculator] Base: {baseDamage}, Mask: {attackerMask}, vs {targetFamily}/{targetElement}");

            float multiplier = BASE_MULTIPLIER;
            bool affinityBonus = false;
            bool elementCounter = false;

            // Check affinity bonus (mask vs family) - WRONG per GDD!
            if (CheckAffinityCounter(attackerMask, targetFamily))
            {
                multiplier *= AFFINITY_MULTIPLIER;
                affinityBonus = true;
                Debug.Log($"[DamageCalculator] ⚠️ Affinity via Mask ({attackerMask} > {targetFamily}): {multiplier}x");
            }

            // Check element counter (mask element vs enemy element)
            if (CheckElementCounter(attackerMask, targetElement))
            {
                multiplier *= ELEMENT_MULTIPLIER;
                elementCounter = true;
                Debug.Log($"[DamageCalculator] ✅ Element Counter Applied ({attackerMask} > {targetElement}): {multiplier}x");
            }

            float finalDamage = baseDamage * multiplier;
            Debug.Log($"[DamageCalculator] FINAL: {baseDamage} x {multiplier} = {finalDamage}");

            return new DamageResult
            {
                BaseDamage = baseDamage,
                FinalDamage = finalDamage,
                Multiplier = multiplier,
                AffinityBonus = affinityBonus,
                ElementCounter = elementCounter,
                WasLethal = false
            };
        }

        /// <summary>
        /// Check if mask affinity counters enemy family (GDD-specified)
        /// WARNING: This should use PlayerAffinity, not mask!
        /// Fire -> Trolls, Lightning -> Scorpion Men, Earth -> Gargoyles
        /// </summary>
        private static bool CheckAffinityCounter(MaskType mask, EnemyFamily family)
        {
            bool isCounter = (mask, family) switch
            {
                (MaskType.Fire, EnemyFamily.Troll) => true,
                (MaskType.Lightning, EnemyFamily.ScorpionMan) => true,
                (MaskType.Earth, EnemyFamily.Gargoyle) => true,
                _ => false
            };

            Debug.Log($"[DamageCalculator] Affinity Counter Check: {mask} vs {family} = {isCounter}");
            return isCounter;
        }

        /// <summary>
        /// Check if mask element counters enemy element (GDD-specified counters)
        /// Fire Mask > Ice enemies
        /// Lightning Mask > Metal enemies
        /// Earth Mask > Rock enemies
        /// </summary>
        private static bool CheckElementCounter(MaskType mask, EnemyElement element)
        {
            bool isCounter = (mask, element) switch
            {
                (MaskType.Fire, EnemyElement.Ice) => true,         // GDD: Fire > Ice
                (MaskType.Lightning, EnemyElement.Metal) => true,  // GDD: Lightning > Metal
                (MaskType.Earth, EnemyElement.Rock) => true,       // GDD: Earth > Rock
                _ => false
            };

            Debug.Log($"[DamageCalculator] Element Counter Check: {mask} vs {element} = {isCounter}");
            return isCounter;
        }

        /// <summary>
        /// Get recommended mask for maximum damage against enemy
        /// Prioritizes affinity match, but returns 4x combination if possible
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
                EnemyElement.Ice => MaskType.Fire,         // GDD: Fire > Ice
                EnemyElement.Metal => MaskType.Lightning,  // GDD: Lightning > Metal
                EnemyElement.Rock => MaskType.Earth,       // GDD: Earth > Rock
                _ => MaskType.Fire
            };

            // If affinity mask also counters element, use it (4x damage)
            if (CheckElementCounter(affinityMask, element))
            {
                Debug.Log($"[DamageCalculator] Optimal Mask: {affinityMask} (4x combo vs {family}/{element})");
                return affinityMask;
            }

            // Otherwise prefer affinity (family counter is more important per GDD)
            Debug.Log($"[DamageCalculator] Optimal Mask: {affinityMask} (affinity vs {family}, element would be {elementMask})");
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

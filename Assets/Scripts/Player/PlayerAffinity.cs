using UnityEngine;
using Unity.Netcode;
using MaskBound.Core.Enums;
using System.Collections.Generic;

namespace MaskBound.Player
{
    /// <summary>
    /// Manages a player's permanent affinity (GDD-specified)
    /// 
    /// GDD: "Each player has a permanent affinity that provides passive bonuses  
    /// (e.g., increased damage or stagger) against one enemy family.
    /// Affinity is always active and cannot be changed."
    /// 
    /// This is SEPARATE from masks (which are swappable).
    /// Affinity is assigned based on spawn order or weapon type.
    /// </summary>
    public class PlayerAffinity : NetworkBehaviour
    {
        [Header("Permanent Affinity (GDD)")]
        [Tooltip("Which enemy family this player deals bonus damage to. Cannot be changed after spawn.")]
        [SerializeField] private EnemyFamily affinityTarget;

        /// <summary>
        /// Read-only access to this player's permanent affinity
        /// </summary>
        public EnemyFamily AffinityTarget => affinityTarget;

        // Server-side pool of available affinities (static so it persists across all player instances)
        private static List<EnemyFamily> availableAffinities = new List<EnemyFamily>();
        private static bool poolInitialized = false;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                AssignRandomUniqueAffinity();
            }
            
            Debug.Log($"[PlayerAffinity] Client {OwnerClientId} spawned with affinity: {affinityTarget}");
        }

        /// <summary>
        /// Assign a random affinity from the available pool (server-side only)
        /// Ensures no two players have the same affinity
        /// Pool resets when all affinities are assigned (3 players)
        /// </summary>
        private void AssignRandomUniqueAffinity()
        {
            // Initialize pool with all 3 affinities if empty or first time
            if (!poolInitialized || availableAffinities.Count == 0)
            {
                availableAffinities = new List<EnemyFamily>
                {
                    EnemyFamily.Troll,
                    EnemyFamily.ScorpionMan,
                    EnemyFamily.Gargoyle
                };
                poolInitialized = true;
                Debug.Log("[PlayerAffinity] Server initialized affinity pool: [Troll, ScorpionMan, Gargoyle]");
            }

            // Pick random affinity from available pool
            int randomIndex = Random.Range(0, availableAffinities.Count);
            affinityTarget = availableAffinities[randomIndex];
            
            // Remove from pool so it's not assigned to another player
            availableAffinities.RemoveAt(randomIndex);
            
            Debug.Log($"[PlayerAffinity] Server randomly assigned {affinityTarget} to Client {OwnerClientId}");
            Debug.Log($"[PlayerAffinity] Remaining affinities: [{string.Join(", ", availableAffinities)}]");
        }

        /// <summary>
        /// Call this when server shuts down or resets to clear the affinity pool
        /// </summary>
        public static void ResetAffinityPool()
        {
            availableAffinities.Clear();
            poolInitialized = false;
            Debug.Log("[PlayerAffinity] Affinity pool reset");
        }

        /// <summary>
        /// Check if this player's affinity counters the given enemy family
        /// Returns true for 2x damage multiplier
        /// </summary>
        public bool CountersFamily(EnemyFamily family)
        {
            bool counters = affinityTarget == family;
            Debug.Log($"[PlayerAffinity] Client {OwnerClientId}: Affinity {affinityTarget} vs {family} = {counters}");
            return counters;
        }

        /// <summary>
        /// Get a string description of this affinity for UI display
        /// </summary>
        public string GetAffinityDescription()
        {
            return affinityTarget switch
            {
                EnemyFamily.Troll => "Troll Hunter - Bonus damage vs Trolls",
                EnemyFamily.ScorpionMan => "Scorpion Slayer - Bonus damage vs Scorpion Men",
                EnemyFamily.Gargoyle => "Gargoyle Bane - Bonus damage vs Gargoyles",
                _ => "Unknown Affinity"
            };
        }
    }
}

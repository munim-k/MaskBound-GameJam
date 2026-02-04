using UnityEngine;
using MaskBound.Core.Data;
using MaskBound.Core.Enums;

namespace MaskBound.Core.Interfaces
{
    /// <summary>
    /// Interface for entities that can receive damage.
    /// Implement this on players, enemies, destructible objects, etc.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Apply damage to this entity from a specific source
        /// </summary>
        /// <param name="amount">Amount of damage to apply</param>
        /// <param name="source">Information about damage source</param>
        void TakeDamage(float amount, DamageSource source);
        
        /// <summary>
        /// Current health value
        /// </summary>
        float CurrentHealth { get; }
        
        /// <summary>
        /// Maximum health value
        /// </summary>
        float MaxHealth { get; }
        
        /// <summary>
        /// Is this entity currently alive?
        /// </summary>
        bool IsAlive { get; }
    }

    /// <summary>
    /// Interface for entities that can be revived (players only)
    /// </summary>
    public interface IRevivable
    {
        /// <summary>
        /// Revive this entity with specified health percentage
        /// </summary>
        /// <param name="healthPercent">Health to restore (0-1)</param>
        void Revive(float healthPercent);
        
        /// <summary>
        /// Is this entity currently in knocked/downed state?
        /// </summary>
        bool IsKnocked { get; }
        
        /// <summary>
        /// Can this entity be revived right now?
        /// </summary>
        bool CanBeRevived { get; }
    }

    /// <summary>
    /// Interface for entities with elemental affinity
    /// </summary>
    public interface IHasAffinity
    {
        /// <summary>
        /// Which enemy family this entity counters effectively
        /// </summary>
        EnemyFamily AffinityCounter { get; }
        
        /// <summary>
        /// Current mask equipped (determines affinity)
        /// </summary>
        MaskType CurrentMask { get; }
    }

    /// <summary>
    /// Interface for entities that can be corrupted by mask usage
    /// </summary>
    public interface ICorruptible
    {
        /// <summary>
        /// Current corruption level (0-1, where 1 is fully corrupted)
        /// </summary>
        float CorruptionLevel { get; }
        
        /// <summary>
        /// Apply corruption damage over time
        /// </summary>
        void ApplyCorruption(float amount);
        
        /// <summary>
        /// Reduce corruption (via mask swap or heal station)
        /// </summary>
        void ReduceCorruption(float amount);
        
        /// <summary>
        /// Is corruption actively damaging the entity?
        /// </summary>
        bool IsCorrupted { get; }
    }
}

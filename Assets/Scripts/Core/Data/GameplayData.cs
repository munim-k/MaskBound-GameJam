using UnityEngine;
using MaskBound.Core.Enums;

namespace MaskBound.Core.Data
{
    /// <summary>
    /// Information about a damage event
    /// </summary>
    public struct DamageSource
    {
        /// <summary>
        /// Client ID of the attacker (if player)
        /// </summary>
        public ulong? AttackerClientId;
        
        /// <summary>
        /// Type of damage being applied
        /// </summary>
        public DamageType DamageType;
        
        /// <summary>
        /// World position where damage was dealt
        /// </summary>
        public Vector3 HitPoint;
        
        /// <summary>
        /// Hit normal (for knockback direction)
        /// </summary>
        public Vector3 HitNormal;
        
        /// <summary>
        /// Mask used by attacker (for affinity calculation)
        /// </summary>
        public MaskType? AttackerMask;
        
        /// <summary>
        /// Enemy element being hit (for affinity calculation)
        /// </summary>
        public EnemyElement? TargetElement;

        /// <summary>
        /// Was this a critical hit?
        /// </summary>
        public bool IsCritical;
    }

    /// <summary>
    /// Complete damage calculation result
    /// </summary>
    public struct DamageResult
    {
        /// <summary>
        /// Base damage before multipliers
        /// </summary>
        public float BaseDamage;
        
        /// <summary>
        /// Final damage after all calculations
        /// </summary>
        public float FinalDamage;
        
        /// <summary>
        /// Damage multiplier applied (1.0 = normal, 2.0 = double, etc.)
        /// </summary>
        public float Multiplier;
        
        /// <summary>
        /// Was affinity bonus applied?
        /// </summary>
        public bool AffinityBonus;
        
        /// <summary>
        /// Was element counter applied?
        /// </summary>
        public bool ElementCounter;
        
        /// <summary>
        /// Hit resulted in death?
        /// </summary>
        public bool WasLethal;
    }

    /// <summary>
    /// Player state snapshot for prediction/reconciliation
    /// </summary>
    public struct PlayerStateSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public float Timestamp;
        public uint TickNumber;
    }

    /// <summary>
    /// Enemy spawn configuration
    /// </summary>
    public struct EnemySpawnData
    {
        public EnemyType Type;
        public EnemyElement Element;
        public Vector3 SpawnPosition;
        public Quaternion SpawnRotation;
        public float HealthMultiplier;
        public float DamageMultiplier;
    }
}

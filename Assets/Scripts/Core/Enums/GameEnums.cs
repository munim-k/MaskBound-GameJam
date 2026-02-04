namespace MaskBound.Core.Enums
{
    /// <summary>
    /// Type of damage being applied
    /// </summary>
    public enum DamageType
    {
        /// <summary>
        /// Melee weapon attacks (sword, hammer, etc.)
        /// </summary>
        Melee,
        
        /// <summary>
        /// Ranged weapon attacks (bow, gun, etc.)
        /// </summary>
        Ranged,
        
        /// <summary>
        /// Magic/ability damage
        /// </summary>
        Magic,
        
        /// <summary>
        /// Environmental hazards (lava, spikes, fall damage)
        /// </summary>
        Environmental,
        
        /// <summary>
        /// Corruption damage over time
        /// </summary>
        Corruption
    }

    /// <summary>
    /// Player state machine states
    /// </summary>
    public enum PlayerState
    {
        /// <summary>
        /// Standing still, no actions
        /// </summary>
        Idle,
        
        /// <summary>
        /// Moving around
        /// </summary>
        Moving,
        
        /// <summary>
        /// Performing attack
        /// </summary>
        Attacking,
        
        /// <summary>
        /// Stunned by enemy attack
        /// </summary>
        Stunned,
        
        /// <summary>
        /// Grabbed by troll
        /// </summary>
        Grabbed,
        
        /// <summary>
        /// Knocked down (can be revived)
        /// </summary>
        Knocked,
        
        /// <summary>
        /// Reviving another player
        /// </summary>
        Reviving,
        
        /// <summary>
        /// Dead (no revives left, game over condition)
        /// </summary>
        Dead,
        
        /// <summary>
        /// In arena safe zone/downtime
        /// </summary>
        Safe
    }

    /// <summary>
    /// Enemy type (base family)
    /// </summary>
    public enum EnemyType
    {
        // Troll Family
        Orc,
        
        // Scorpion Man Family
        ScorpionMan,
        
        // Gargoyle Family
        Gargoyle
    }

    /// <summary>
    /// Enemy elemental variant (GDD-specified)
    /// These elements are countered by specific masks for 2x damage multiplier
    /// </summary>
    public enum EnemyElement
    {
        /// <summary>
        /// Ice element - Countered by Fire Mask (2x damage)
        /// Applies slow debuff to players
        /// Example: Ice Troll, Ice Scorpion Man, Ice Gargoyle
        /// </summary>
        Ice,
        
        /// <summary>
        /// Metal element - Countered by Lightning Mask (2x damage)
        /// High defense stat
        /// Example: Metal Troll, Metal Scorpion Man, Metal Gargoyle
        /// </summary>
        Metal,
        
        /// <summary>
        /// Rock element - Countered by Earth Mask (2x damage)
        /// High poise stat (harder to stagger)
        /// Example: Rock Troll, Rock Scorpion Man, Rock Gargoyle
        /// </summary>
        Rock
    }

    /// <summary>
    /// Player affinity (determined by mask)
    /// Counters specific enemy families
    /// </summary>
    public enum PlayerAffinity
    {
        /// <summary>
        /// Fire mask - Counters Trolls
        /// </summary>
        Fire,
        
        /// <summary>
        /// Lightning mask - Counters Scorpion Men
        /// </summary>
        Lightning,
        
        /// <summary>
        /// Earth mask - Counters Gargoyles
        /// </summary>
        Earth
    }

    /// <summary>
    /// Enemy family (for affinity counter system)
    /// </summary>
    public enum EnemyFamily
    {
        /// <summary>
        /// Orc family (countered by Fire affinity)
        /// GDD calls this "Troll" but using "Orc" for consistency
        /// </summary>
        Orc,
        
        /// <summary>
        /// Scorpion Man family (countered by Lightning affinity)
        /// </summary>
        ScorpionMan,
        
        /// <summary>
        /// Gargoyle family (countered by Earth affinity)
        /// </summary>
        Gargoyle
    }

    /// <summary>
    /// Arena/game phase
    /// </summary>
    public enum GamePhase
    {
        /// <summary>
        /// Lobby/matchmaking
        /// </summary>
        Lobby,
        
        /// <summary>
        /// Face upload phase
        /// </summary>
        Upload,
        
        /// <summary>
        /// Loading into game
        /// </summary>
        Loading,
        
        /// <summary>
        /// Safe zone between arenas
        /// </summary>
        Downtime,
        
        /// <summary>
        /// Active combat in arena
        /// </summary>
        Combat,
        
        /// <summary>
        /// Victory screen
        /// </summary>
        Victory,
        
        /// <summary>
        /// Defeat screen
        /// </summary>
        Defeat
    }
}

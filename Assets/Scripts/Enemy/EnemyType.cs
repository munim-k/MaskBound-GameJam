using FMODUnity;
using UnityEngine;


public class EnemyType : MonoBehaviour
{
    public enum Type
    {
        Orc,
        ScorpionMan,
        Gargoyle
    }
    public Type enemyType;

    public EventReference GetWalkReference()
    {
        switch (enemyType)
        {
            case Type.Orc:
                return FMODEvents.instance.trollWalk;
            case Type.ScorpionMan:
                return FMODEvents.instance.scorpionWalk;
            case Type.Gargoyle:
                return FMODEvents.instance.gargoyleFly;
            default:
                Debug.LogError("Unknown enemy type for walk SFX");
                return default;
        }
    }

    public EventReference GetDeathReference()
    {
        switch (enemyType)
        {
            case Type.Orc:
                return FMODEvents.instance.trollDeath;
            case Type.ScorpionMan:
                return FMODEvents.instance.scorpionDeath;
            case Type.Gargoyle:
                return FMODEvents.instance.gargoyleDeath;
            default:
                Debug.LogError("Unknown enemy type for death SFX");
                return default;
        }
    }
}
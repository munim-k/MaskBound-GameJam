using FMODUnity;
using MaskBound.Enemy;
using UnityEngine;

public class EnemyFamilyReference : MonoBehaviour
{
    public enum FamilyAudioType
    {
        Troll,
        Gargoyle,
        Scorpion
    }

    public FamilyAudioType familyAudioType;

    public EventReference GetDeathReference()
    {
        switch (familyAudioType)
        {
            case FamilyAudioType.Troll:
                return FMODEvents.instance.trollDeath;
            case FamilyAudioType.Gargoyle:
                return FMODEvents.instance.gargoyleDeath;
            case FamilyAudioType.Scorpion:
                return FMODEvents.instance.scorpionDeath;
            default:
                Debug.LogError("Enemy family not recognized for death sound.");
                return default;
        }
    }

    public EventReference GetWalkReference()
    {
        switch (familyAudioType)
        {
            case FamilyAudioType.Troll:
                return FMODEvents.instance.trollWalk;
            case FamilyAudioType.Gargoyle:
                return FMODEvents.instance.gargoyleFly;
            case FamilyAudioType.Scorpion:
                return FMODEvents.instance.scorpionWalk;
            default:
                Debug.LogError("Enemy family not recognized for walk sound.");
                return default;
        }
    }
}

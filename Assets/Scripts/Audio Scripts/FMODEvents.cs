using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using System.Runtime.InteropServices;

public class FMODEvents : MonoBehaviour
{
    [field: Header("Ambience")]
    [field: SerializeField] public EventReference ambience { get; private set; }

    [field: Header("Music")]
    [field: SerializeField] public EventReference music { get; private set; }

    [field: Header("Player SFX")]
    [field: SerializeField] public EventReference playerFootsteps { get; private set; }
    [field: SerializeField] public EventReference maskSwap { get; private set; }
    [field: SerializeField] public EventReference playerJump { get; private set; }
    [field: SerializeField] public EventReference playerLand { get; private set; }
    [field: SerializeField] public EventReference playerLowHealth { get; private set; }
    [field: SerializeField] public EventReference playerSlide { get; private set; }
    [field: SerializeField] public EventReference playerDeath { get; private set; }
    [field: SerializeField] public EventReference playerHurt { get; private set; }

    [field: Header("Troll SFX")]
    [field: SerializeField] public EventReference trollWalk { get; private set; }
    [field: SerializeField] public EventReference trollRange { get; private set; }
    [field: SerializeField] public EventReference trollMelee { get; private set; }
    [field: SerializeField] public EventReference trollDeath { get; private set; }

    [field: Header("Gargoyle SFX")]
    [field: SerializeField] public EventReference gargoyleFly { get; private set; }
    [field: SerializeField] public EventReference gargoyleScreech { get; private set; }
    [field: SerializeField] public EventReference gargoyleMelee { get; private set; }
    [field: SerializeField] public EventReference gargoyleDeath { get; private set; }

    [field: Header("Scorpion SFX")]
    [field: SerializeField] public EventReference scorpionWalk { get; private set; }
    [field: SerializeField] public EventReference scorpionMelee { get; private set; }
    [field: SerializeField] public EventReference scorpionRange { get; private set; }
    [field: SerializeField] public EventReference scorpionDeath { get; private set; }

    [field: Header("UI SFX")]
    [field: SerializeField] public EventReference uiClick { get; private set; }
    [field: SerializeField] public EventReference requestReceive { get; private set; }

    [field: Header("Attacks SFX")]
    [field: SerializeField] public EventReference weakHit { get; private set; }
    [field: SerializeField] public EventReference strongHit { get; private set; }
    [field: SerializeField] public EventReference swordSwing { get; private set; }

    [field: SerializeField] public EventReference fogWall { get; private set; }

    public static FMODEvents instance { get; private set; }

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than one FMOD Events instance in the scene.");
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum EnemyFamily
{
    Orc,
    ScorpionMan,
    Gargoyle
}

public enum EnemySubElement
{
    None,
    Ice,
    Metal,
    Rock
}

public enum SpawnPointMode
{
    ByIndex,
    Random,
    RoundRobin
}

[DisallowMultipleComponent]
public class EnemySpawner : NetworkBehaviour
{
    [Header("Spawn Points")] [Tooltip("Possible spawn locations inside the arena.")] [SerializeField]
    private Transform[] spawnPoints;

    [Header("Enemy Prefabs (Networked)")]
    [Tooltip("Each prefab MUST have a NetworkObject and be registered in NetworkManager prefab list.")]
    [SerializeField]
    private GameObject[] enemyGameObjects;

    [SerializeField] private GameObject[] Orcs;
    [SerializeField] private GameObject[] ScorpionMen;
    [SerializeField] private GameObject[] Gargoyles;

    private EnemyPrefabEntry[] enemyPrefabs;

    [Header("Arena Sequences (Scripted, not waves)")]
    [Tooltip("Arena-based scripted spawns. Arena ends when all spawned enemies are dead.")]
    [SerializeField]
    private ArenaSequence[] arenas;

    [Header("Runtime")] [SerializeField] private bool autoStartArenaOnServer = false;
    [SerializeField] private int startArenaIndex = 0;

    // Alive tracking (server authority)
    private readonly HashSet<ulong> _aliveEnemyNetIds = new HashSet<ulong>();
    private int _roundRobinIndex = 0;
    private Coroutine _runRoutine;
    public DoorOpening[] doors;
    public event Action<int> OnArenaStartedServer; // arenaIndex
    public event Action<int> OnArenaCompletedServer; // arenaIndex
    public event Action<int, int> OnAliveCountChangedServer; // arenaIndex, alive

    public int CurrentArenaIndex { get; private set; } = -1;

    private bool isArenaStarted = false;
    private float minValue = 0f;
    private float maxValue = 3f;
    private float increaseRate = 0.05f;
    private float decreaseRate = 0.33f;
    private float currentValue = 0f;

    #region Inspector structs

    private void Start()
    {
        if (enemyGameObjects == null || enemyGameObjects.Length == 0)
        {
            Debug.LogError("[EnemySpawner] enemyGameObjects is empty. Assign prefabs in inspector.");
            enemyPrefabs = Array.Empty<EnemyPrefabEntry>();
            return;
        }

        enemyPrefabs = new EnemyPrefabEntry[enemyGameObjects.Length];

        for (int i = 0; i < enemyGameObjects.Length; i++)
        {
            var go = enemyGameObjects[i];
            if (go == null)
            {
                Debug.LogError($"[EnemySpawner] enemyGameObjects[{i}] is null.");
                continue;
            }

            EnemyFamily enemytag;

            // Prefer explicit mapping, avoid relying on tag if you can.
            // If you keep tags, make sure these tags exist in Unity Tag Manager.
            var tag = go.tag;

            if (tag == "Orc") enemytag = EnemyFamily.Orc;
            else if (tag == "Scorpion") enemytag = EnemyFamily.ScorpionMan;
            else if (tag == "Gargoyle") enemytag = EnemyFamily.Gargoyle;
            else
            {
                Debug.LogError($"[EnemySpawner] Prefab {go.name} has unknown tag '{tag}'.");
                continue;
            }

            enemyPrefabs[i] = new EnemyPrefabEntry
            {
                family = enemytag,
                prefab = go
            };
        }
    }


    [Serializable]
    public struct EnemyPrefabEntry
    {
        public EnemyFamily family;

        [Tooltip("Prefab with NetworkObject + enemy scripts.")]
        public GameObject prefab; // ✅ Inspector-friendly
    }


    [Serializable]
    public class ArenaSequence
    {
        public string arenaName = "Arena";

        [Tooltip("Optional delay before arena begins.")]
        public float arenaIntroDelay = 0f;

        [Tooltip("Steps spawn in order. This is how you create 'learning moments'.")]
        public Step[] steps;
    }

    [Serializable]
    public class Step
    {
        [Header("Spawn Spec")] public EnemyFamily family;
        public EnemySubElement element = EnemySubElement.None;

        [Min(1)] public int count = 1;

        [Tooltip("Delay before this step executes.")]
        public float delayBefore = 0f;

        [Tooltip("Time between each spawn inside this step.")]
        public float spawnInterval = 0.25f;

        [Header("Spawn Point Selection")] public SpawnPointMode spawnPointMode = SpawnPointMode.Random;

        [Tooltip("Used when SpawnPointMode = ByIndex")]
        public int spawnPointIndex = 0;

        [Header("Step Flow")]
        [Tooltip("If true, the sequence waits until all enemies currently alive are dead before moving to next step.")]
        public bool waitUntilAllDeadBeforeNextStep = false;

        [Tooltip("Optional: show a debug log when the step runs.")]
        public bool debugLog = true;
    }

    #endregion

    // =========================================================
    // NGO
    // =========================================================

    public override void OnNetworkSpawn()
    {
        if (IsServer && autoStartArenaOnServer)
        {
            StartArenaServer(startArenaIndex);
        }
    }

    // ✅ Added: subscribe to despawn notifications (server-side)
    private void OnEnable()
    {
        EnemyLifetimeReporter.OnEnemyDespawnedServer += OnEnemyDespawnedServer;
    }

    private void OnDisable()
    {
        EnemyLifetimeReporter.OnEnemyDespawnedServer -= OnEnemyDespawnedServer;
    }

    // =========================================================
    // Public API
    // =========================================================

    public void StartArenaServer(int arenaIndex)
    {
        if (!IsServer) return;
        if (arenas == null || arenas.Length == 0) return;
        arenaIndex = Mathf.Clamp(arenaIndex, 0, arenas.Length - 1);

        StopArenaServer();

        CurrentArenaIndex = arenaIndex;
        _runRoutine = StartCoroutine(RunArenaSequenceCoroutine(arenas[arenaIndex], arenaIndex));
    }

    public void StopArenaServer()
    {
        if (!IsServer) return;

        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
        }
    }

    public NetworkObject SpawnAt(EnemyFamily family, EnemySubElement element, Transform point)
    {
        if (!IsServer) return null;

        var prefab = GetPrefab(family, element);
        if (prefab == null)
        {
            Debug.LogError($"[EnemySpawner] Missing prefab for {family}");
            return null;
        }

        var spawned = Instantiate(prefab, point.position, point.rotation);
        var netObj = spawned.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError($"Enemy prefab {prefab.name} is missing NetworkObject!");
            Destroy(spawned);
            return null;
        }

        netObj.Spawn(true);
        RegisterAlive(netObj);


        return netObj;
    }

    // =========================================================
    // Sequence runner
    // =========================================================

    private IEnumerator RunArenaSequenceCoroutine(ArenaSequence arena, int arenaIndex)
    {
        if (arena.arenaIntroDelay > 0f)
            yield return new WaitForSeconds(arena.arenaIntroDelay);

        OnArenaStartedServer?.Invoke(arenaIndex);
        isArenaStarted = true;

        if (arena.steps == null) yield break;

        for (int i = 0; i < arena.steps.Length; i++)
        {
            Step step = arena.steps[i];

            if (step.delayBefore > 0f)
                yield return new WaitForSeconds(step.delayBefore);

            if (step.debugLog)
                Debug.Log($"[EnemySpawner] Arena {arenaIndex} Step {i} → {step.family} {step.element} x{step.count}");

            for (int n = 0; n < step.count; n++)
            {
                Transform p = PickSpawnPoint(step);
                SpawnAt(step.family, step.element, p);

                if (step.spawnInterval > 0f)
                    yield return new WaitForSeconds(step.spawnInterval);
            }

            if (step.waitUntilAllDeadBeforeNextStep)
            {
                while (_aliveEnemyNetIds.Count > 0)
                    yield return null;
            }
        }

        while (_aliveEnemyNetIds.Count > 0)
            yield return null;
        print("All enemies defeated in arena " + arenaIndex);
        OnArenaCompletedServer?.Invoke(arenaIndex);
        isArenaStarted = false;
        for(int i=0;i<doors.Length;i++)
        {
            doors[i].OpenDoor(arenaIndex);
        }
        _runRoutine = null;
    }

    // =========================================================
    // Spawn point selection
    // =========================================================

    private Transform PickSpawnPoint(Step step)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return transform;

        switch (step.spawnPointMode)
        {
            case SpawnPointMode.ByIndex:
                return spawnPoints[Mathf.Clamp(step.spawnPointIndex, 0, spawnPoints.Length - 1)];

            case SpawnPointMode.RoundRobin:
                _roundRobinIndex = (_roundRobinIndex + 1) % spawnPoints.Length;
                return spawnPoints[_roundRobinIndex];

            case SpawnPointMode.Random:
            default:
                return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        }
    }

    // =========================================================
    // Alive tracking (UPDATED: despawn-safe)
    // =========================================================

    private void RegisterAlive(NetworkObject enemyObj)
    {
        _aliveEnemyNetIds.Add(enemyObj.NetworkObjectId);

        // ✅ Ensure the enemy has a reporter to notify despawn on server
        var reporter = enemyObj.GetComponent<EnemyLifetimeReporter>();
        if (reporter == null)
        {
            reporter = enemyObj.gameObject.AddComponent<EnemyLifetimeReporter>();
        }
        
        reporter.TrackedNetId = enemyObj.NetworkObjectId;

        OnAliveCountChangedServer?.Invoke(CurrentArenaIndex, _aliveEnemyNetIds.Count);
    }

    // ✅ Called when any enemy NetworkObject despawns on the server
    private void OnEnemyDespawnedServer(ulong netId)
    {
        if (!IsServer) return;

        _aliveEnemyNetIds.Remove(netId);
        OnAliveCountChangedServer?.Invoke(CurrentArenaIndex, _aliveEnemyNetIds.Count);
    }

    // =========================================================
    // Prefab lookup
    // =========================================================

    private GameObject GetPrefab(EnemyFamily family, EnemySubElement element)
    {
        GameObject[] targetArray = null;

        // 1. Select the correct Family array
        switch (family)
        {
            case EnemyFamily.Orc: targetArray = Orcs; break;
            case EnemyFamily.ScorpionMan: targetArray = ScorpionMen; break;
            case EnemyFamily.Gargoyle: targetArray = Gargoyles; break;
        }

        if (targetArray == null || targetArray.Length == 0) return null;

        // 2. Return based on Element index
        // Mapping: 0=None/Basic, 1=Ice, 2=Metal, 3=Rock
        switch (element)
        {
            case EnemySubElement.None:  return targetArray[0];
            case EnemySubElement.Ice:   return targetArray[1];
            case EnemySubElement.Metal: return targetArray[2];
            case EnemySubElement.Rock:  return targetArray[3];
            default: return targetArray[0];
        }
    }

    /// <summary>
    /// Add this to enemy prefabs (or EnemySpawner adds it at runtime).
    /// It fires when the NetworkObject is despawned, which is the correct NGO lifecycle hook.
    /// </summary>
    public class EnemyLifetimeReporter : MonoBehaviour
    {
        public static event Action<ulong> OnEnemyDespawnedServer;
        public ulong TrackedNetId;

        // OnDestroy is called when the GameObject is destroyed (Despawn calls Destroy)
        private void OnDestroy()
        {
            // Verify we are on server is implicit if we only add this on server, 
            // but checking IsServer here is hard since component is being destroyed.
            // We rely on the fact that we only added this component on the server in RegisterAlive.
            if (TrackedNetId != 0)
            {
                OnEnemyDespawnedServer?.Invoke(TrackedNetId);
            }
        }
    }

    private void Update()
    {
        if(isArenaStarted)
        {
            currentValue += increaseRate * Time.deltaTime;
            currentValue = Mathf.Clamp(currentValue, minValue, maxValue);
            AudioManager.instance.SetMusicParameter("MusicDifficulty", currentValue);
        }
        else
        {
            currentValue -= decreaseRate * Time.deltaTime;
            currentValue = Mathf.Clamp(currentValue, minValue, maxValue);
            AudioManager.instance.SetMusicParameter("MusicDifficulty", currentValue);
        }
    }
}

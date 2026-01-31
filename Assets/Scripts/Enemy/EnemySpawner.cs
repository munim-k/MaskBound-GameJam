using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum EnemyFamily
{
    Troll,
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
    [Header("Spawn Points")]
    [Tooltip("Possible spawn locations inside the arena.")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Enemy Prefabs (Networked)")]
    [Tooltip("Each prefab MUST have a NetworkObject and be registered in NetworkManager prefab list.")]
    [SerializeField] private EnemyPrefabEntry[] enemyPrefabs;

    [Header("Arena Sequences (Scripted, not waves)")]
    [Tooltip("Arena-based scripted spawns. Arena ends when all spawned enemies are dead.")]
    [SerializeField] private ArenaSequence[] arenas;

    [Header("Runtime")]
    [SerializeField] private bool autoStartArenaOnServer = false;
    [SerializeField] private int startArenaIndex = 0;

    // Alive tracking (server authority)
    private readonly HashSet<ulong> _aliveEnemyNetIds = new HashSet<ulong>();
    private int _roundRobinIndex = 0;
    private Coroutine _runRoutine;

    public event Action<int> OnArenaStartedServer;           // arenaIndex
    public event Action<int> OnArenaCompletedServer;         // arenaIndex
    public event Action<int, int> OnAliveCountChangedServer; // arenaIndex, alive

    public int CurrentArenaIndex { get; private set; } = -1;

    #region Inspector structs

    [Serializable]
    public struct EnemyPrefabEntry
    {
        public EnemyFamily family;
        [Tooltip("Prefab with NetworkObject + enemy scripts. Element can be applied at runtime.")]
        public NetworkObject prefab;
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
        [Header("Spawn Spec")]
        public EnemyFamily family;
        public EnemySubElement element = EnemySubElement.None;

        [Min(1)] public int count = 1;

        [Tooltip("Delay before this step executes.")]
        public float delayBefore = 0f;

        [Tooltip("Time between each spawn inside this step.")]
        public float spawnInterval = 0.25f;

        [Header("Spawn Point Selection")]
        public SpawnPointMode spawnPointMode = SpawnPointMode.Random;

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

        var prefab = GetPrefab(family);
        if (prefab == null)
        {
            Debug.LogError($"[EnemySpawner] Missing prefab for {family}");
            return null;
        }

        var spawned = Instantiate(prefab, point.position, point.rotation);
        spawned.Spawn(true);

        RegisterAlive(spawned);

        var elementReceiver = spawned.GetComponent<IEnemyElementReceiver>();
        elementReceiver?.ServerSetElement(element);

        return spawned;
    }

    // =========================================================
    // Sequence runner
    // =========================================================

    private IEnumerator RunArenaSequenceCoroutine(ArenaSequence arena, int arenaIndex)
    {
        if (arena.arenaIntroDelay > 0f)
            yield return new WaitForSeconds(arena.arenaIntroDelay);

        OnArenaStartedServer?.Invoke(arenaIndex);

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

        OnArenaCompletedServer?.Invoke(arenaIndex);
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
        if (enemyObj.GetComponent<EnemyLifetimeReporter>() == null)
        {
            enemyObj.gameObject.AddComponent<EnemyLifetimeReporter>();
        }

        OnAliveCountChangedServer?.Invoke(CurrentArenaIndex, _aliveEnemyNetIds.Count);
    }

    // ✅ Called when any enemy NetworkObject despawns on the server
    private void OnEnemyDespawnedServer(NetworkObject netObj)
    {
        if (!IsServer) return;

        _aliveEnemyNetIds.Remove(netObj.NetworkObjectId);
        OnAliveCountChangedServer?.Invoke(CurrentArenaIndex, _aliveEnemyNetIds.Count);
    }

    // =========================================================
    // Prefab lookup
    // =========================================================

    private NetworkObject GetPrefab(EnemyFamily family)
    {
        if (enemyPrefabs == null) return null;
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i].family == family)
                return enemyPrefabs[i].prefab;
        }
        return null;
    }
}

/// <summary>
/// Add this to enemy prefabs (or EnemySpawner adds it at runtime).
/// It fires when the NetworkObject is despawned, which is the correct NGO lifecycle hook.
/// </summary>
public class EnemyLifetimeReporter : NetworkBehaviour
{
    public static event Action<NetworkObject> OnEnemyDespawnedServer;

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        OnEnemyDespawnedServer?.Invoke(NetworkObject);
    }
}

/// <summary>
/// Optional interface: put this on your enemy prefab if you want element variants
/// to be applied at spawn time (e.g. set VFX, defense, poise).
/// </summary>
public interface IEnemyElementReceiver
{
    void ServerSetElement(EnemySubElement element);
}

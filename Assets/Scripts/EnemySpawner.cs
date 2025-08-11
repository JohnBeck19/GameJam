using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class Spawnable
    {
        public GameObject prefab;
        [Tooltip("Base weight for selection when eligible.")]
        public float baseWeight = 1f;
        [Tooltip("Minimum difficulty required before this can spawn.")]
        public float minDifficulty = 0f;
        [Tooltip("Extra weight per difficulty unit (adds to base weight).")]
        public float weightPerDifficulty = 0.25f;
        [Tooltip("Spawn a group in this size range.")]
        public Vector2Int groupSizeRange = new Vector2Int(1, 1);
    }

    [Header("Refs")]
    [SerializeField] private Transform player;
    [Tooltip("Optional fixed spawn points. If empty, spawns at this spawner's position.")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Header("Spawnables")]
    [SerializeField] private List<Spawnable> spawnables = new List<Spawnable>();

    [Header("Difficulty")] 
    [Tooltip("Initial difficulty at scene start.")]
    [SerializeField, Range(0f, 100f)] private float startingDifficulty = 0f;
    [Tooltip("Extra starting difficulty per previous exit (uses GameManager.ExitCount if present).")]
    [SerializeField] private float extraDifficultyPerExit = 0.5f;
    [Tooltip("Difficulty gained each second.")]
    [SerializeField] private float difficultyRampPerSecond = 0.03f;
    [Tooltip("Optional clamp for difficulty; set <= 0 for unlimited.")]
    [SerializeField] private float maxDifficulty = 10f;

    [Header("Concurrency & Rate")] 
    [Tooltip("Target enemies at difficulty 0.")]
    [SerializeField] private int baseConcurrent = 3;
    [Tooltip("How many more enemies per +1 difficulty.")]
    [SerializeField] private float concurrentPerDifficulty = 2f;
    [SerializeField] private int maxConcurrent = 30;

    [Tooltip("Spawn interval at difficulty 0 (seconds).")]
    [SerializeField] private float baseSpawnInterval = 3.0f;
    [Tooltip("Minimum possible spawn interval regardless of difficulty.")]
    [SerializeField] private float minSpawnInterval = 0.4f;
    [Tooltip("How strongly difficulty shortens the interval. Effective interval = max(min, base / (1 + diff * factor))")]
    [SerializeField] private float intervalDifficultyFactor = 0.6f;

    [Header("Placement")] 
    [Tooltip("Keep spawns at least this far from the player (if player is set).")]
    [SerializeField] private float minDistanceFromPlayer = 5f;
    [Tooltip("Try this many random spawn points before giving up this spawn tick.")]
    [SerializeField] private int placementAttempts = 8;
    [Tooltip("Ground tilemaps where enemies are allowed to spawn. If empty, auto-detect Tilemaps named 'Floor' or 'Ground'.")]
    [SerializeField] private List<Tilemap> groundTilemaps = new List<Tilemap>();
    [Tooltip("Attempts to sample a random ground cell each spawn tick.")]
    [SerializeField] private int groundSampleAttempts = 40;

    [Header("Fallback Area Sampling")]
    [Tooltip("If no valid ground tile position or spawn point is found, attempt to sample inside the camera's visible bounds.")]
    [SerializeField] private bool fallbackToCameraBounds = true;
    [Tooltip("Extra world-space padding around camera bounds used for fallback sampling.")]
    [SerializeField] private float cameraBoundsPadding = 2f;

    private readonly List<GameObject> _alive = new List<GameObject>();
    private float _difficulty;
    private float _spawnTimer;

    private void Awake()
    {
        if (player == null)
        {
            var p = FindFirstObjectByType<Player>();
            if (p != null) player = p.transform;
        }

        _difficulty = startingDifficulty;

        var gm = FindFirstObjectByType<GameManager>();
        if (gm != null && extraDifficultyPerExit > 0f)
        {
            _difficulty += extraDifficultyPerExit * Mathf.Max(0, gm.ExitCount);
        }
        if (maxDifficulty > 0f)
            _difficulty = Mathf.Min(_difficulty, maxDifficulty);

        _spawnTimer = GetCurrentInterval();
    }

    private void Update()
    {
        // Remove destroyed references
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            if (_alive[i] == null)
                _alive.RemoveAt(i);
        }

        // Ramp difficulty
        if (difficultyRampPerSecond > 0f)
        {
            _difficulty += difficultyRampPerSecond * Time.deltaTime;
            if (maxDifficulty > 0f)
                _difficulty = Mathf.Min(_difficulty, maxDifficulty);
        }

        int targetConcurrent = Mathf.Clamp(baseConcurrent + Mathf.FloorToInt(_difficulty * concurrentPerDifficulty), 0, maxConcurrent);

        // Tick timer
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = GetCurrentInterval();
            if (_alive.Count < targetConcurrent)
            {
                TrySpawn();
            }
        }
    }

    private float GetCurrentInterval()
    {
        float denom = 1f + Mathf.Max(0f, _difficulty) * Mathf.Max(0f, intervalDifficultyFactor);
        float interval = baseSpawnInterval / denom;
        return Mathf.Max(minSpawnInterval, interval);
    }

    private void TrySpawn()
    {
        var entry = PickSpawnable();
        if (entry == null || entry.prefab == null)
            return;

        int group = Mathf.Clamp(Random.Range(entry.groupSizeRange.x, entry.groupSizeRange.y + 1), 1, 50);

        for (int i = 0; i < group; i++)
        {
            Vector3? pos = FindSpawnPosition();
            if (!pos.HasValue)
                break;

            var go = Instantiate(entry.prefab, pos.Value, Quaternion.identity);
            _alive.Add(go);
        }
    }

    private Spawnable PickSpawnable()
    {
        // Build weighted list of eligible entries
        List<(Spawnable s, float w)> pool = new List<(Spawnable, float)>();
        for (int i = 0; i < spawnables.Count; i++)
        {
            var s = spawnables[i];
            if (s == null || s.prefab == null)
                continue;
            if (_difficulty < s.minDifficulty)
                continue;
            float w = Mathf.Max(0.0001f, s.baseWeight + s.weightPerDifficulty * _difficulty);
            pool.Add((s, w));
        }

        if (pool.Count == 0)
            return null;

        float total = 0f;
        for (int i = 0; i < pool.Count; i++) total += pool[i].w;
        float r = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            acc += pool[i].w;
            if (r <= acc)
                return pool[i].s;
        }
        return pool[pool.Count - 1].s;
    }

    private Vector3? FindSpawnPosition()
    {
        // Prefer ground tilemaps if available
        Vector3? groundPos = FindRandomGroundPosition();
        if (groundPos.HasValue)
            return groundPos.Value;

        // If we have spawn points, pick among those respecting player distance
        if (spawnPoints != null && spawnPoints.Count > 0)
        {
            for (int attempt = 0; attempt < Mathf.Max(1, placementAttempts); attempt++)
            {
                var t = spawnPoints[Random.Range(0, spawnPoints.Count)];
                if (t == null) continue;
                if (IsFarEnoughFromPlayer(t.position))
                    return t.position;
            }
            // No valid spawn point respecting player radius this tick
            // Fall through to fallback sampling
        }

        // Fallback: sample inside camera bounds if enabled
        if (fallbackToCameraBounds)
        {
            Vector3? camPos = FindRandomCameraPosition();
            if (camPos.HasValue)
                return camPos.Value;
        }

        // Default: spawn at this spawner's position if acceptable
        if (IsFarEnoughFromPlayer(transform.position))
            return transform.position;
        return null;
    }

    private Vector3? FindRandomGroundPosition()
    {
        EnsureGroundTilemaps();
        if (groundTilemaps == null || groundTilemaps.Count == 0)
            return null;

        for (int attempt = 0; attempt < Mathf.Max(1, groundSampleAttempts); attempt++)
        {
            Tilemap tm = groundTilemaps[Random.Range(0, groundTilemaps.Count)];
            if (tm == null) continue;
            BoundsInt cb = tm.cellBounds;
            if (cb.size.x <= 0 || cb.size.y <= 0) continue;
            int cx = Random.Range(cb.xMin, cb.xMax);
            int cy = Random.Range(cb.yMin, cb.yMax);
            Vector3Int cell = new Vector3Int(cx, cy, 0);
            if (!tm.HasTile(cell))
                continue;
            Vector3 world = tm.GetCellCenterWorld(cell);
            if (IsFarEnoughFromPlayer(world))
                return world;
        }
        return null;
    }

    private void EnsureGroundTilemaps()
    {
        if (groundTilemaps != null && groundTilemaps.Count > 0)
            return;
        // Auto-detect common floor names
        var all = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (groundTilemaps == null)
            groundTilemaps = new List<Tilemap>();
        foreach (var tm in all)
        {
            if (tm == null) continue;
            string n = tm.name.ToLowerInvariant();
            if (n.Contains("floor") || n.Contains("ground"))
            {
                groundTilemaps.Add(tm);
            }
        }
        // If no matching names were found, include all tilemaps as a fallback
        if (groundTilemaps.Count == 0)
        {
            for (int i = 0; i < all.Length; i++)
            {
                var tm = all[i];
                if (tm != null) groundTilemaps.Add(tm);
            }
        }
    }

    private bool IsFarEnoughFromPlayer(Vector3 position)
    {
        if (player == null || minDistanceFromPlayer <= 0f)
            return true;
        return (position - player.position).sqrMagnitude >= (minDistanceFromPlayer * minDistanceFromPlayer);
    }

    private Vector3? FindRandomCameraPosition()
    {
        var cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return null;

        float height = cam.orthographicSize * 2f + cameraBoundsPadding * 2f;
        float width = height * cam.aspect;
        Vector3 center = cam.transform.position;
        Vector3 min = new Vector3(center.x - width * 0.5f, center.y - height * 0.5f, 0f);
        Vector3 max = new Vector3(center.x + width * 0.5f, center.y + height * 0.5f, 0f);

        for (int attempt = 0; attempt < Mathf.Max(1, placementAttempts); attempt++)
        {
            float x = Random.Range(min.x, max.x);
            float y = Random.Range(min.y, max.y);
            Vector3 pos = new Vector3(x, y, 0f);
            if (IsFarEnoughFromPlayer(pos))
                return pos;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (player != null && minDistanceFromPlayer > 0f)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawWireSphere(player.position, minDistanceFromPlayer);
        }
    }
}



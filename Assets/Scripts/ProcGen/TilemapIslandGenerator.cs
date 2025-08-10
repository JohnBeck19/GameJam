using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProcGen
{
    [ExecuteAlways]
    public class TilemapIslandGenerator : MonoBehaviour
    {
        [Header("Tilemaps")]
        public Tilemap floorTilemap;
        public Tilemap wallTilemap;

        [Header("Grid Size")]
        public int width = 128;
        public int height = 128;

        [Header("Noise Settings")]
        public int seed = 0;
        [Min(0.0001f)] public float scale = 50f;
        [Range(1, 10)] public int octaves = 4;
        [Range(0f, 1f)] public float persistence = 0.5f;
        [Min(1f)] public float lacunarity = 2f;
        public Vector2 noiseOffset;

        [Header("Falloff")]
        [Range(0f, 1f)] public float falloffStrength = 0.75f; // 0 disables
        public float falloffA = 3f;
        public float falloffB = 2.2f;

        [Header("Tiles & Thresholds")]
        public TileBase floorTile;
        public TileBase wallTile;
        [Range(0f, 1f)] public float floorThreshold = 0.5f;

        [Header("Options")]
        public bool autoRegenerateInEditMode = false;
        public bool clearBeforePaint = true;
        [Tooltip("If enabled, only paint walls at the boundary between floor and non-floor.")]
        public bool paintEdgeWallsOnly = true;
        [Tooltip("If true, use 8 neighbors for edge detection; otherwise 4 (N,S,E,W).")]
        public bool useEightWayNeighbors = true;
        [Tooltip("If assigned, this tile is used for edges. Otherwise the generic wall tile is used.")]
        public TileBase edgeWallTile;

        [Header("Directional Walls")]
        [Tooltip("Choose specific wall/corner tiles based on adjacent floor direction(s). Applies only to edge cells.")]
        public bool enableDirectionalWalls = true;
        public TileBase wallNorth;
        public TileBase wallSouth;
        public TileBase wallEast;
        public TileBase wallWest;
        public TileBase cornerNE;
        public TileBase cornerNW;
        public TileBase cornerSE;
        public TileBase cornerSW;

        [Header("Generation & Spawn")]
        [Tooltip("Regenerate map when play starts.")]
        public bool regenerateOnStart = true;
        [Tooltip("Randomize seed on play start before generating.")]
        public bool randomizeSeedOnStart = true;
        [Tooltip("After generating, automatically select a spawn point and place/move the player.")]
        public bool spawnAfterGenerate = true;
        [Tooltip("Existing player transform to move to the spawn point. If null and a prefab is assigned, a new instance will be spawned.")]
        public Transform playerTransform;
        [Tooltip("If set and no existing player is provided, this prefab will be instantiated at the spawn point.")]
        public GameObject playerPrefab;
        [Range(0, 5)] public int spawnClearanceRadius = 1;
        [Tooltip("Prefer a spawn close to the center if possible.")]
        public bool preferCenterSpawn = true;

        private bool[,] lastFloorMask;

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            lacunarity = Mathf.Max(1f, lacunarity);

            if (autoRegenerateInEditMode && !Application.isPlaying)
            {
                Generate();
            }
        }

        private void Awake()
        {
            EnsureTilemaps();
        }

        private void Reset()
        {
            EnsureTilemaps();
        }

        private void EnsureTilemaps()
        {
            // If not assigned, try to use a Tilemap on the same GameObject as the floor layer by default.
            if (floorTilemap == null)
            {
                floorTilemap = GetComponent<Tilemap>();
            }
        }

        private void Start()
        {
            if (Application.isPlaying && regenerateOnStart)
            {
                if (randomizeSeedOnStart)
                {
                    seed = Random.Range(0, int.MaxValue);
                }
                Generate();
            }
        }

        [ContextMenu("Generate")]
        public void Generate()
        {
            EnsureTilemaps();

            float[,] noise = NoiseMapGenerator.GeneratePerlinNoiseMap(
                width, height, seed, scale, octaves, persistence, lacunarity, noiseOffset);

            if (falloffStrength > 0f)
            {
                float[,] falloff = FalloffMapGenerator.GenerateRadialFalloff(width, height, falloffA, falloffB);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float f = falloff[x, y] * falloffStrength;
                        noise[x, y] = Mathf.Clamp01(noise[x, y] - f);
                    }
                }
            }

            if (clearBeforePaint)
            {
                if (floorTilemap != null) floorTilemap.ClearAllTiles();
                if (wallTilemap != null) wallTilemap.ClearAllTiles();
            }

            Vector3Int origin = new Vector3Int(-width / 2, -height / 2, 0);
            // Precompute floor mask
            bool[,] isFloorMask = new bool[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    isFloorMask[x, y] = noise[x, y] >= floorThreshold;
                }
            }
            lastFloorMask = isFloorMask;

            // Paint floor and walls
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isFloor = isFloorMask[x, y];
                    Vector3Int pos = new Vector3Int(origin.x + x, origin.y + y, 0);

                    // Floor layer
                    if (floorTilemap != null)
                    {
                        floorTilemap.SetTile(pos, isFloor ? floorTile : null);
                    }

                    // Wall layer
                    if (wallTilemap != null)
                    {
                        if (isFloor)
                        {
                            wallTilemap.SetTile(pos, null);
                        }
                        else
                        {
                            bool hasFloorNeighbor = HasFloorNeighbor(isFloorMask, x, y, useEightWayNeighbors);
                            if (paintEdgeWallsOnly)
                            {
                                if (hasFloorNeighbor)
                                {
                                    TileBase tileToUse = edgeWallTile != null ? edgeWallTile : wallTile;
                                    if (enableDirectionalWalls)
                                    {
                                        tileToUse = SelectDirectionalWallTile(isFloorMask, x, y) ?? tileToUse;
                                    }
                                    wallTilemap.SetTile(pos, tileToUse);
                                }
                                else
                                {
                                    wallTilemap.SetTile(pos, null);
                                }
                            }
                            else
                            {
                                wallTilemap.SetTile(pos, wallTile);
                            }
                        }
                    }
                }
            }

            if (spawnAfterGenerate)
            {
                SpawnPlayerAtBestLocation();
            }
        }

        private TileBase SelectDirectionalWallTile(bool[,] floorMask, int x, int y)
        {
            // Determine floor neighbors in 4 directions
            bool n = IsFloorAt(floorMask, x, y + 1);
            bool s = IsFloorAt(floorMask, x, y - 1);
            bool e = IsFloorAt(floorMask, x + 1, y);
            bool w = IsFloorAt(floorMask, x - 1, y);

            int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);

            // Single neighbor: use facing tile
            if (count == 1)
            {
                if (n && wallNorth != null) return wallNorth;
                if (s && wallSouth != null) return wallSouth;
                if (e && wallEast != null) return wallEast;
                if (w && wallWest != null) return wallWest;
            }

            // Two neighbors
            if (count == 2)
            {
                // Corner cases (orthogonal pairs)
                if (n && e && cornerNE != null) return cornerNE;
                if (n && w && cornerNW != null) return cornerNW;
                if (s && e && cornerSE != null) return cornerSE;
                if (s && w && cornerSW != null) return cornerSW;

                // Opposite neighbors -> choose a reasonable default
                if (n && s)
                {
                    if (wallNorth != null) return wallNorth;
                    if (wallSouth != null) return wallSouth;
                }
                if (e && w)
                {
                    if (wallEast != null) return wallEast;
                    if (wallWest != null) return wallWest;
                }
            }

            // Three or four neighbors: fall back to generic edge or base wall
            return null;
        }

        private bool IsFloorAt(bool[,] floorMask, int x, int y)
        {
            int w = floorMask.GetLength(0);
            int h = floorMask.GetLength(1);
            if (x < 0 || x >= w || y < 0 || y >= h) return false;
            return floorMask[x, y];
        }

        private bool HasFloorNeighbor(bool[,] floorMask, int x, int y, bool eightWay)
        {
            int width = floorMask.GetLength(0);
            int height = floorMask.GetLength(1);
            // 4-way neighbors
            int[][] dirs4 = new int[][]
            {
                new[]{ 1, 0 }, new[]{ -1, 0 }, new[]{ 0, 1 }, new[]{ 0, -1 }
            };
            // 8-way adds diagonals
            int[][] dirs8 = new int[][]
            {
                new[]{ 1, 0 }, new[]{ -1, 0 }, new[]{ 0, 1 }, new[]{ 0, -1 },
                new[]{ 1, 1 }, new[]{ -1, 1 }, new[]{ 1, -1 }, new[]{ -1, -1 }
            };

            var dirs = eightWay ? dirs8 : dirs4;
            for (int i = 0; i < dirs.Length; i++)
            {
                int nx = x + dirs[i][0];
                int ny = y + dirs[i][1];
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (floorMask[nx, ny]) return true;
                }
            }
            return false;
        }

        private void SpawnPlayerAtBestLocation()
        {
            if (lastFloorMask == null) return;
            Vector3Int? spawnCellOpt = FindSpawnCell(lastFloorMask, spawnClearanceRadius, preferCenterSpawn);
            if (!spawnCellOpt.HasValue) return;

            Vector3Int spawnCell = spawnCellOpt.Value;
            // Use floor tilemap if available for world conversion, else wall tilemap
            Tilemap tm = floorTilemap != null ? floorTilemap : wallTilemap;
            if (tm == null) return;
            Vector3 worldPos = tm.GetCellCenterWorld(spawnCell);

            if (playerTransform != null)
            {
                playerTransform.position = worldPos;
            }
            else if (playerPrefab != null)
            {
                var instance = GameObject.Instantiate(playerPrefab, worldPos, Quaternion.identity);
                playerTransform = instance.transform;
            }
        }

        private Vector3Int? FindSpawnCell(bool[,] floorMask, int clearanceRadius, bool centerFirst)
        {
            int w = floorMask.GetLength(0);
            int h = floorMask.GetLength(1);

            // Helper to test a cell
            bool IsClear(int cx, int cy)
            {
                for (int dy = -clearanceRadius; dy <= clearanceRadius; dy++)
                {
                    for (int dx = -clearanceRadius; dx <= clearanceRadius; dx++)
                    {
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) return false;
                        if (!floorMask[nx, ny]) return false;
                    }
                }
                return true;
            }

            // Try center first
            if (centerFirst)
            {
                int cx = w / 2;
                int cy = h / 2;
                if (IsClear(cx, cy))
                {
                    Vector3Int origin = new Vector3Int(-w / 2, -h / 2, 0);
                    return new Vector3Int(origin.x + cx, origin.y + cy, 0);
                }
            }

            // Random sampling
            int attempts = Mathf.Min(2000, w * h);
            for (int i = 0; i < attempts; i++)
            {
                int rx = Random.Range(0, w);
                int ry = Random.Range(0, h);
                if (IsClear(rx, ry))
                {
                    Vector3Int origin = new Vector3Int(-w / 2, -h / 2, 0);
                    return new Vector3Int(origin.x + rx, origin.y + ry, 0);
                }
            }

            // Fallback: first floor found
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (floorMask[x, y])
                    {
                        Vector3Int origin = new Vector3Int(-w / 2, -h / 2, 0);
                        return new Vector3Int(origin.x + x, origin.y + y, 0);
                    }
                }
            }

            return null;
        }
    }
}



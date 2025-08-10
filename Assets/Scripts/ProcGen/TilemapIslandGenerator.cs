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
        [Tooltip("Optional: separate tilemap for foliage/decoration placed on top of the floor.")]
        public Tilemap foliageTilemap;

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
        [Tooltip("Optional: if provided, a variant will be chosen per floor cell.")]
        public TileBase[] floorTiles;
        public TileBase wallTile;
        [Range(0f, 1f)] public float floorThreshold = 0.5f;

        [Header("Foliage Settings")]
        [Tooltip("Tiles used as foliage/decoration. One will be chosen per placed cell.")]
        public TileBase[] foliageTiles;
        [Tooltip("Optional weights matching foliageTiles for weighted random selection.")]
        public float[] foliageTileWeights;
        [Tooltip("Overall chance to place foliage on an eligible floor cell when not using Perlin noise.")]
        [Range(0f, 1f)] public float foliageDensity = 0.15f;
        [Tooltip("Use Perlin noise to create natural foliage patches.")]
        public bool usePerlinForFoliage = true;
        [Min(0.0001f)] public float foliageScale = 10f;
        public int foliageSeedOffset = 9001;
        public Vector2 foliageOffset;
        [Range(1, 8)] public int foliageOctaves = 1;
        [Range(0f, 1f)] public float foliagePersistence = 0.5f;
        [Min(1f)] public float foliageLacunarity = 2f;
        [Tooltip("Normalized threshold on foliage noise (higher = sparser). Only used if Perlin is enabled.")]
        [Range(0f, 1f)] public float foliageNoiseThreshold = 0.6f;
        [Tooltip("Blend amount with deterministic hash-based randomness (0 = pure Perlin, 1 = pure random). Only used if Perlin is enabled.")]
        [Range(0f, 1f)] public float foliageHashBlend = 0f;
        [Tooltip("Avoid placing foliage on tiles adjacent to non-floor (near walls/edges).")]
        public bool avoidEdgesForFoliage = true;
        [Tooltip("Clear foliage in this radius around the chosen spawn cell.")]
        [Range(0, 4)] public int spawnFoliageClearanceRadius = 1;
        [Tooltip("Apply a random rotation to foliage tiles for visual variety.")]
        public bool foliageRandomRotation = true;
        [Tooltip("If enabled, foliage rotation is snapped to 0/90/180/270 degrees. If disabled, uses any 0..360 degrees.")]
        public bool foliageUseRightAngles = true;
        [Tooltip("Apply a random scale to foliage tiles for visual variety.")]
        public bool foliageRandomScale = false;
        [Tooltip("Use the same random scale on X and Y.")]
        public bool foliageUseUniformScale = true;
        [Tooltip("Uniform random scale range (min to max). Used when Uniform Scale is enabled.")]
        public Vector2 foliageUniformScaleRange = new Vector2(0.9f, 1.1f);
        [Tooltip("Random X scale range (min to max). Used when Uniform Scale is disabled.")]
        public Vector2 foliageScaleXRange = new Vector2(0.9f, 1.1f);
        [Tooltip("Random Y scale range (min to max). Used when Uniform Scale is disabled.")]
        public Vector2 foliageScaleYRange = new Vector2(0.9f, 1.1f);

        [Header("Foliage Layer 2 Settings")]
        [Tooltip("Optional: separate tilemap for a second foliage/decoration layer.")]
        public Tilemap foliage2Tilemap;
        [Tooltip("Tiles used as foliage for layer 2.")]
        public TileBase[] foliage2Tiles;
        [Tooltip("Optional weights matching foliage2Tiles for weighted random selection.")]
        public float[] foliage2TileWeights;
        [Tooltip("Overall chance to place layer-2 foliage on an eligible floor cell when Perlin is disabled.")]
        [Range(0f, 1f)] public float foliage2Density = 0.07f;
        [Tooltip("Use Perlin noise to create natural foliage patches for layer 2.")]
        public bool usePerlinForFoliage2 = true;
        [Min(0.0001f)] public float foliage2Scale = 9f;
        public int foliage2SeedOffset = 9002;
        public Vector2 foliage2Offset;
        [Range(1, 8)] public int foliage2Octaves = 1;
        [Range(0f, 1f)] public float foliage2Persistence = 0.5f;
        [Min(1f)] public float foliage2Lacunarity = 2f;
        [Tooltip("Normalized threshold on foliage-2 noise (higher = sparser). Only used if Perlin is enabled.")]
        [Range(0f, 1f)] public float foliage2NoiseThreshold = 0.65f;
        [Tooltip("Blend amount with deterministic hash-based randomness for layer 2 (0 = pure Perlin, 1 = pure random). Only used if Perlin is enabled.")]
        [Range(0f, 1f)] public float foliage2HashBlend = 0f;
        [Tooltip("Avoid placing layer-2 foliage on tiles adjacent to non-floor (near walls/edges).")]
        public bool avoidEdgesForFoliage2 = true;
        [Tooltip("Apply a random rotation to layer-2 foliage.")]
        public bool foliage2RandomRotation = true;
        [Tooltip("Snap layer-2 foliage rotation to 0/90/180/270.")]
        public bool foliage2UseRightAngles = true;
        [Tooltip("Apply a random scale to layer-2 foliage.")]
        public bool foliage2RandomScale = false;
        [Tooltip("Use the same random scale on X and Y for layer 2.")]
        public bool foliage2UseUniformScale = true;
        public Vector2 foliage2UniformScaleRange = new Vector2(0.9f, 1.1f);
        public Vector2 foliage2ScaleXRange = new Vector2(0.9f, 1.1f);
        public Vector2 foliage2ScaleYRange = new Vector2(0.9f, 1.1f);

        [Header("Floor Variant Noise")]
        [Tooltip("Use a Perlin noise map to pick floor variants (organic patches). If disabled, falls back to deterministic hashing.")]
        public bool usePerlinForFloorVariants = true;
        [Min(0.0001f)] public float floorVariantScale = 12f;
        public int floorVariantSeedOffset = 1337;
        public Vector2 floorVariantOffset;
        [Range(1, 8)] public int floorVariantOctaves = 1;
        [Range(0f, 1f)] public float floorVariantPersistence = 0.5f;
        [Min(1f)] public float floorVariantLacunarity = 2f;
        [Tooltip("Clamp and remap the variant noise before choosing a tile. Useful to bias towards specific variants.")]
        [Range(0f, 1f)] public float floorVariantClampMin = 0f;
        [Range(0f, 1f)] public float floorVariantClampMax = 1f;
        [Tooltip("Optional curve to remap 0..1 noise into a biased distribution for picking variants.")]
        public AnimationCurve floorVariantRemap = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Blend amount with deterministic hash-based randomness (0 = pure noise, 1 = pure hash).")]
        [Range(0f, 1f)] public float floorVariantHashBlend = 0f;
        [Tooltip("Optional weights matching floorTiles; used to map noise to a weighted distribution.")]
        public float[] floorTileWeights;

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
        private float[,] lastVariantNoise;
        private float[,] lastFoliageNoise;
        private float[,] lastFoliage2Noise;

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            lacunarity = Mathf.Max(1f, lacunarity);
            floorVariantLacunarity = Mathf.Max(1f, floorVariantLacunarity);
            if (floorVariantClampMax < floorVariantClampMin)
            {
                var tmp = floorVariantClampMax;
                floorVariantClampMax = floorVariantClampMin;
                floorVariantClampMin = tmp;
            }
            if (floorVariantRemap == null || floorVariantRemap.length == 0)
            {
                floorVariantRemap = AnimationCurve.Linear(0, 0, 1, 1);
            }

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
                if (foliageTilemap != null) foliageTilemap.ClearAllTiles();
                if (foliage2Tilemap != null) foliage2Tilemap.ClearAllTiles();
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

            // Precompute floor variant noise if requested
            if (usePerlinForFloorVariants && floorTiles != null && floorTiles.Length > 0)
            {
                lastVariantNoise = NoiseMapGenerator.GeneratePerlinNoiseMap(
                    width, height, seed + floorVariantSeedOffset, floorVariantScale,
                    floorVariantOctaves, floorVariantPersistence, floorVariantLacunarity, floorVariantOffset);
            }
            else
            {
                lastVariantNoise = null;
            }

            // Precompute foliage noise if requested
            if (usePerlinForFoliage)
            {
                lastFoliageNoise = NoiseMapGenerator.GeneratePerlinNoiseMap(
                    width, height, seed + foliageSeedOffset, foliageScale,
                    foliageOctaves, foliagePersistence, foliageLacunarity, foliageOffset);
            }
            else
            {
                lastFoliageNoise = null;
            }

            // Precompute foliage2 noise if requested
            if (usePerlinForFoliage2)
            {
                lastFoliage2Noise = NoiseMapGenerator.GeneratePerlinNoiseMap(
                    width, height, seed + foliage2SeedOffset, foliage2Scale,
                    foliage2Octaves, foliage2Persistence, foliage2Lacunarity, foliage2Offset);
            }
            else
            {
                lastFoliage2Noise = null;
            }

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
                        if (isFloor)
                        {
                            TileBase chosenFloor = GetFloorTileForCell(x, y);
                            floorTilemap.SetTile(pos, chosenFloor);
                        }
                        else
                        {
                            floorTilemap.SetTile(pos, null);
                        }
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

                    // Foliage layer
                    if (foliageTilemap != null)
                    {
                        if (isFloor && ShouldPlaceFoliageAt(isFloorMask, x, y))
                        {
                            TileBase foliage = GetFoliageTileForCell(x, y);
                            foliageTilemap.SetTile(pos, foliage);
                            float angle = GetFoliageRotationDegreesForCell(x, y);
                            Vector3 scale = GetFoliageScaleForCell(x, y);
                            Matrix4x4 xfm = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, angle), scale);
                            foliageTilemap.SetTransformMatrix(pos, xfm);
                        }
                        else
                        {
                            foliageTilemap.SetTile(pos, null);
                            foliageTilemap.SetTransformMatrix(pos, Matrix4x4.identity);
                        }
                    }

                    // Foliage layer 2
                    if (foliage2Tilemap != null)
                    {
                        if (isFloor && ShouldPlaceFoliage2At(isFloorMask, x, y))
                        {
                            TileBase foliage = GetFoliage2TileForCell(x, y);
                            foliage2Tilemap.SetTile(pos, foliage);
                            float angle = GetFoliage2RotationDegreesForCell(x, y);
                            Vector3 scale = GetFoliage2ScaleForCell(x, y);
                            Matrix4x4 xfm = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, angle), scale);
                            foliage2Tilemap.SetTransformMatrix(pos, xfm);
                        }
                        else
                        {
                            foliage2Tilemap.SetTile(pos, null);
                            foliage2Tilemap.SetTransformMatrix(pos, Matrix4x4.identity);
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

        private TileBase GetFloorTileForCell(int x, int y)
        {
            // Choose a tile deterministically per-cell if variants are provided
            if (floorTiles != null && floorTiles.Length > 0)
            {
                // If a variant noise map is available, map noise value [0..1] to an index
                if (usePerlinForFloorVariants && lastVariantNoise != null)
                {
                    float v = lastVariantNoise[x, y]; // 0..1
                    // Clamp and normalize window
                    v = Mathf.Clamp01(v);
                    if (floorVariantClampMax > floorVariantClampMin)
                    {
                        v = Mathf.InverseLerp(floorVariantClampMin, floorVariantClampMax, v);
                    }
                    // Remap via curve
                    if (floorVariantRemap != null && floorVariantRemap.length > 0)
                    {
                        v = Mathf.Clamp01(floorVariantRemap.Evaluate(v));
                    }
                    // Blend with hash-based randomness
                    if (floorVariantHashBlend > 0f)
                    {
                        float hash01 = Hash01(x, y, seed);
                        v = Mathf.Lerp(v, hash01, floorVariantHashBlend);
                    }

                    int idx = SelectVariantIndexFromValue(v, floorTiles.Length, floorTileWeights);
                    return floorTiles[idx] != null ? floorTiles[idx] : floorTile;
                }
                // Fallback: deterministic hash by cell
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + x;
                    hash = hash * 31 + y;
                    if (seed != 0)
                        hash = hash * 31 + seed;
                    int idx = Mathf.Abs(hash) % floorTiles.Length;
                    return floorTiles[idx] != null ? floorTiles[idx] : floorTile;
                }
            }
            return floorTile;
        }

        private int SelectVariantIndexFromValue(float value01, int count, float[] weights)
        {
            if (count <= 1) return 0;
            if (weights == null || weights.Length != count)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(value01 * count), 0, count - 1);
                return idx;
            }
            // Normalize weights and build cumulative distribution
            float total = 0f;
            for (int i = 0; i < count; i++) total += Mathf.Max(0f, weights[i]);
            if (total <= 0f)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt(value01 * count), 0, count - 1);
                return idx;
            }
            float threshold = value01 * total;
            float accum = 0f;
            for (int i = 0; i < count; i++)
            {
                accum += Mathf.Max(0f, weights[i]);
                if (threshold <= accum)
                {
                    return i;
                }
            }
            return count - 1;
        }

        private float Hash01(int x, int y, int seedVal)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + seedVal;
                uint uh = (uint)hash;
                // Jenkins mix for better spread
                uh += (uh << 10);
                uh ^= (uh >> 6);
                uh += (uh << 3);
                uh ^= (uh >> 11);
                uh += (uh << 15);
                return (uh & 0xFFFFFF) / (float)0x1000000; // 0..1
            }
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

        private bool IsNearNonFloor(bool[,] floorMask, int x, int y, bool eightWay)
        {
            int width = floorMask.GetLength(0);
            int height = floorMask.GetLength(1);
            int[][] dirs4 = new int[][]
            {
                new[]{ 1, 0 }, new[]{ -1, 0 }, new[]{ 0, 1 }, new[]{ 0, -1 }
            };
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
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) return true;
                if (!floorMask[nx, ny]) return true;
            }
            return false;
        }

        private bool ShouldPlaceFoliageAt(bool[,] floorMask, int x, int y)
        {
            if (foliageTiles == null || foliageTiles.Length == 0) return false;
            if (avoidEdgesForFoliage && IsNearNonFloor(floorMask, x, y, true)) return false;

            if (usePerlinForFoliage && lastFoliageNoise != null)
            {
                float v = Mathf.Clamp01(lastFoliageNoise[x, y]);
                if (foliageHashBlend > 0f)
                {
                    float hash01 = Hash01(x, y, seed + foliageSeedOffset);
                    v = Mathf.Lerp(v, hash01, foliageHashBlend);
                }
                return v >= foliageNoiseThreshold;
            }

            float r = Hash01(x, y, seed + foliageSeedOffset);
            return r < foliageDensity;
        }

        private TileBase GetFoliageTileForCell(int x, int y)
        {
            if (foliageTiles == null || foliageTiles.Length == 0) return null;
            int count = foliageTiles.Length;
            // Use a different salt than placement to avoid biasing accepted cells toward low indices
            const int selectionSalt = 0x5F3759DF; // arbitrary large salt
            float r = Hash01(x, y, seed + foliageSeedOffset + selectionSalt);
            int idx = SelectVariantIndexFromValue(r, count, foliageTileWeights);
            return foliageTiles[idx];
        }

        private float GetFoliageRotationDegreesForCell(int x, int y)
        {
            if (!foliageRandomRotation) return 0f;
            float r = Hash01(x, y, seed + foliageSeedOffset + 12345);
            if (foliageUseRightAngles)
            {
                int step = Mathf.FloorToInt(r * 4f) % 4;
                return step * 90f;
            }
            return r * 360f;
        }

        private Vector3 GetFoliageScaleForCell(int x, int y)
        {
            if (!foliageRandomScale)
            {
                return Vector3.one;
            }

            if (foliageUseUniformScale)
            {
                float r = Hash01(x, y, seed + foliageSeedOffset + 54321);
                float s = Mathf.Lerp(
                    Mathf.Min(foliageUniformScaleRange.x, foliageUniformScaleRange.y),
                    Mathf.Max(foliageUniformScaleRange.x, foliageUniformScaleRange.y),
                    r);
                return new Vector3(s, s, 1f);
            }
            else
            {
                float rx = Hash01(x, y, seed + foliageSeedOffset + 11111);
                float ry = Hash01(x, y, seed + foliageSeedOffset + 22222);
                float sx = Mathf.Lerp(
                    Mathf.Min(foliageScaleXRange.x, foliageScaleXRange.y),
                    Mathf.Max(foliageScaleXRange.x, foliageScaleXRange.y),
                    rx);
                float sy = Mathf.Lerp(
                    Mathf.Min(foliageScaleYRange.x, foliageScaleYRange.y),
                    Mathf.Max(foliageScaleYRange.x, foliageScaleYRange.y),
                    ry);
                return new Vector3(sx, sy, 1f);
            }
        }

        private bool ShouldPlaceFoliage2At(bool[,] floorMask, int x, int y)
        {
            if (foliage2Tiles == null || foliage2Tiles.Length == 0) return false;
            if (avoidEdgesForFoliage2 && IsNearNonFloor(floorMask, x, y, true)) return false;

            if (usePerlinForFoliage2 && lastFoliage2Noise != null)
            {
                float v = Mathf.Clamp01(lastFoliage2Noise[x, y]);
                if (foliage2HashBlend > 0f)
                {
                    float hash01 = Hash01(x, y, seed + foliage2SeedOffset);
                    v = Mathf.Lerp(v, hash01, foliage2HashBlend);
                }
                return v >= foliage2NoiseThreshold;
            }

            float r = Hash01(x, y, seed + foliage2SeedOffset);
            return r < foliage2Density;
        }

        private TileBase GetFoliage2TileForCell(int x, int y)
        {
            if (foliage2Tiles == null || foliage2Tiles.Length == 0) return null;
            int count = foliage2Tiles.Length;
            const int selectionSalt = 0x1F2E3D4C; // different salt for layer 2
            float r = Hash01(x, y, seed + foliage2SeedOffset + selectionSalt);
            int idx = SelectVariantIndexFromValue(r, count, foliage2TileWeights);
            return foliage2Tiles[idx];
        }

        private float GetFoliage2RotationDegreesForCell(int x, int y)
        {
            if (!foliage2RandomRotation) return 0f;
            float r = Hash01(x, y, seed + foliage2SeedOffset + 33333);
            if (foliage2UseRightAngles)
            {
                int step = Mathf.FloorToInt(r * 4f) % 4;
                return step * 90f;
            }
            return r * 360f;
        }

        private Vector3 GetFoliage2ScaleForCell(int x, int y)
        {
            if (!foliage2RandomScale)
            {
                return Vector3.one;
            }

            if (foliage2UseUniformScale)
            {
                float r = Hash01(x, y, seed + foliage2SeedOffset + 44444);
                float s = Mathf.Lerp(
                    Mathf.Min(foliage2UniformScaleRange.x, foliage2UniformScaleRange.y),
                    Mathf.Max(foliage2UniformScaleRange.x, foliage2UniformScaleRange.y),
                    r);
                return new Vector3(s, s, 1f);
            }
            else
            {
                float rx = Hash01(x, y, seed + foliage2SeedOffset + 55555);
                float ry = Hash01(x, y, seed + foliage2SeedOffset + 66666);
                float sx = Mathf.Lerp(
                    Mathf.Min(foliage2ScaleXRange.x, foliage2ScaleXRange.y),
                    Mathf.Max(foliage2ScaleXRange.x, foliage2ScaleXRange.y),
                    rx);
                float sy = Mathf.Lerp(
                    Mathf.Min(foliage2ScaleYRange.x, foliage2ScaleYRange.y),
                    Mathf.Max(foliage2ScaleYRange.x, foliage2ScaleYRange.y),
                    ry);
                return new Vector3(sx, sy, 1f);
            }
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

            // Clear foliage around spawn for readability
            if (foliageTilemap != null && spawnFoliageClearanceRadius > 0)
            {
                for (int dy = -spawnFoliageClearanceRadius; dy <= spawnFoliageClearanceRadius; dy++)
                {
                    for (int dx = -spawnFoliageClearanceRadius; dx <= spawnFoliageClearanceRadius; dx++)
                    {
                        Vector3Int c = new Vector3Int(spawnCell.x + dx, spawnCell.y + dy, 0);
                        foliageTilemap.SetTile(c, null);
                        foliageTilemap.SetTransformMatrix(c, Matrix4x4.identity);
                    }
                }
            }

            if (foliage2Tilemap != null && spawnFoliageClearanceRadius > 0)
            {
                for (int dy = -spawnFoliageClearanceRadius; dy <= spawnFoliageClearanceRadius; dy++)
                {
                    for (int dx = -spawnFoliageClearanceRadius; dx <= spawnFoliageClearanceRadius; dx++)
                    {
                        Vector3Int c = new Vector3Int(spawnCell.x + dx, spawnCell.y + dy, 0);
                        foliage2Tilemap.SetTile(c, null);
                        foliage2Tilemap.SetTransformMatrix(c, Matrix4x4.identity);
                    }
                }
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



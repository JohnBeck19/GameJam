using UnityEngine;

namespace ProcGen
{
    public static class NoiseMapGenerator
    {
        public static float[,] GeneratePerlinNoiseMap(
            int width,
            int height,
            int seed,
            float scale,
            int octaves,
            float persistence,
            float lacunarity,
            Vector2 offset)
        {
            float[,] noiseMap = new float[width, height];

            if (scale <= 0f)
            {
                scale = 0.0001f;
            }

            System.Random prng = new System.Random(seed);
            Vector2[] octaveOffsets = new Vector2[octaves];
            for (int i = 0; i < octaves; i++)
            {
                float offsetX = prng.Next(-100000, 100000) + offset.x;
                float offsetY = prng.Next(-100000, 100000) + offset.y;
                octaveOffsets[i] = new Vector2(offsetX, offsetY);
            }

            float maxLocalNoise = float.MinValue;
            float minLocalNoise = float.MaxValue;

            float halfWidth = width / 2f;
            float halfHeight = height / 2f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float amplitude = 1f;
                    float frequency = 1f;
                    float noiseHeight = 0f;

                    for (int i = 0; i < octaves; i++)
                    {
                        float sampleX = ((x - halfWidth) / scale) * frequency + octaveOffsets[i].x;
                        float sampleY = ((y - halfHeight) / scale) * frequency + octaveOffsets[i].y;

                        float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f; // -1..1
                        noiseHeight += perlinValue * amplitude;

                        amplitude *= persistence;
                        frequency *= lacunarity;
                    }

                    if (noiseHeight > maxLocalNoise) maxLocalNoise = noiseHeight;
                    if (noiseHeight < minLocalNoise) minLocalNoise = noiseHeight;

                    noiseMap[x, y] = noiseHeight;
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoise, maxLocalNoise, noiseMap[x, y]);
                }
            }

            return noiseMap;
        }
    }
}



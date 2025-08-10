using UnityEngine;

namespace ProcGen
{
    public static class FalloffMapGenerator
    {
        // Generates an island-style radial falloff, values 0..1 where 1 means full falloff (water)
        public static float[,] GenerateRadialFalloff(int width, int height, float a = 3f, float b = 2.2f)
        {
            float[,] map = new float[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)width * 2f - 1f;   // -1..1
                    float ny = y / (float)height * 2f - 1f;  // -1..1

                    float value = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny)); // square-ish island
                    float falloff = Evaluate(value, a, b);
                    map[x, y] = Mathf.Clamp01(falloff);
                }
            }

            return map;
        }

        private static float Evaluate(float x, float a, float b)
        {
            // From Sebastian Lague's island falloff function
            float powA = Mathf.Pow(x, a);
            float powB = Mathf.Pow(1f - x, b);
            return powA / (powA + powB);
        }
    }
}



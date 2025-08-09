using UnityEngine;

namespace GameJam.Combat
{
    [CreateAssetMenu(menuName = "Combat/Trajectories/Orbit", fileName = "OrbitTrajectory")]
    public class OrbitTrajectory : ProjectileTrajectory
    {
        [Header("Orbit")]
        public float targetRadius = 2f;
        [Tooltip("Time to grow from center to target radius.")]
        public float growthTime = 0.3f;
        [Tooltip("Angular speed in degrees per second. Positive is counter-clockwise.")]
        public float angularSpeedDegPerSec = 90f;
        public bool clockwise = false;

        public override Vector2 EvaluateDisplacement(float timeSeconds, Vector2 initialDirectionNormalized, float speedUnitsPerSecond)
        {
            Vector2 basis = initialDirectionNormalized.sqrMagnitude > 0.000001f ? initialDirectionNormalized.normalized : Vector2.right;
            float radius;
            if (growthTime <= 0.0001f)
            {
                radius = targetRadius;
            }
            else
            {
                radius = Mathf.Lerp(0f, targetRadius, Mathf.Clamp01(timeSeconds / growthTime));
            }

            float sign = clockwise ? -1f : 1f;
            float angle = sign * angularSpeedDegPerSec * timeSeconds;

            // Rotate basis by angle
            float baseAngle = Mathf.Atan2(basis.y, basis.x) * Mathf.Rad2Deg;
            float totalAngle = baseAngle + angle;
            float rad = totalAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        }
    }
} 
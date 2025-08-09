using UnityEngine;

namespace GameJam.Combat
{
    [CreateAssetMenu(menuName = "Combat/Shot Patterns/Orbiting Circle", fileName = "OrbitCirclePattern")]
    public class OrbitCirclePattern : ShotPattern
    {
        [Range(1, 128)]
        public int projectileCount = 8;
        [Header("Circle")]
        public float radius = 2f;
        public float growthTime = 0.3f;
        public float angularSpeedDegPerSec = 90f;
        public bool clockwise = false;
        public float startAngleDeg = 0f;
        public bool alignToFirePoint = true;

        public override void Fire(Transform shooter, Transform firePoint)
        {
            FireWithAngleOffset(shooter, firePoint, 0f);
        }

        public override void FireWithAngleOffset(Transform shooter, Transform firePoint, float globalAngleOffsetDeg)
        {
            if (projectileCount <= 0) return;

            // Create a runtime-configured orbit trajectory instance
            OrbitTrajectory orbit = ScriptableObject.CreateInstance<OrbitTrajectory>();
            orbit.targetRadius = radius;
            orbit.growthTime = growthTime;
            orbit.angularSpeedDegPerSec = angularSpeedDegPerSec;
            orbit.clockwise = clockwise;

            // Temporarily override trajectory for this volley
            ProjectileTrajectory originalTraj = trajectory;
            trajectory = orbit;

            float baseAngle = startAngleDeg;
            if (alignToFirePoint && firePoint != null)
            {
                baseAngle += firePoint.eulerAngles.z;
            }

            float step = 360f / projectileCount;
            Transform origin = firePoint != null ? firePoint : shooter;

            for (int i = 0; i < projectileCount; i++)
            {
                float angle = baseAngle + i * step;
                float finalAngle = ApplyAngleModifiers(angle, i, projectileCount, globalAngleOffsetDeg);
                Vector2 dir = DirFromAngle(finalAngle);
                Spawn(shooter, origin, dir);
            }

            // Restore original trajectory reference
            trajectory = originalTraj;
        }
    }
} 
using UnityEngine;

namespace GameJam.Combat
{
    [CreateAssetMenu(menuName = "Combat/Shot Patterns/Radial", fileName = "RadialShotPattern")]
    public class RadialShotPattern : ShotPattern
    {
        [Range(1, 128)]
        public int projectileCount = 8;
        public float startAngleDeg = 0f;
        public bool alignToFirePoint = true; // rotate startAngle relative to firePoint's rotation

        public override void Fire(Transform shooter, Transform firePoint)
        {
            FireWithAngleOffset(shooter, firePoint, 0f);
        }

        public override void FireWithAngleOffset(Transform shooter, Transform firePoint, float globalAngleOffsetDeg)
        {
            if (projectileCount <= 0) return;

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
        }
    }
} 
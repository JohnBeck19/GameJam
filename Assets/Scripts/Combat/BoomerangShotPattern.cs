using UnityEngine;

namespace GameJam.Combat
{
    [CreateAssetMenu(menuName = "Combat/Shot Patterns/Boomerang", fileName = "BoomerangShotPattern")]
    public class BoomerangShotPattern : ShotPattern
    {
        [Range(1, 64)]
        public int projectileCount = 1;
        [Tooltip("Total arc span in degrees covered by all projectiles. 0 = all stacked on base angle.")]
        public float totalArcDeg = 0f;
        public bool alignToFirePoint = true;

        public override void Fire(Transform shooter, Transform firePoint)
        {
            FireWithAngleOffset(shooter, firePoint, 0f);
        }

        public override void FireWithAngleOffset(Transform shooter, Transform firePoint, float globalAngleOffsetDeg)
        {
            if (projectileCount <= 0) return;

            Transform origin = firePoint != null ? firePoint : shooter;
            float baseAngle = alignToFirePoint && firePoint != null ? firePoint.eulerAngles.z : AngleFromDirection(origin.right);

            if (projectileCount == 1 || Mathf.Approximately(totalArcDeg, 0f))
            {
                float angle = ApplyAngleModifiers(baseAngle, 0, 1, globalAngleOffsetDeg);
                Vector2 dir = DirFromAngle(angle);
                Spawn(shooter, origin, dir);
                return;
            }

            float step = projectileCount > 1 ? (totalArcDeg / (projectileCount - 1)) : 0f;
            float start = baseAngle - totalArcDeg * 0.5f;
            for (int i = 0; i < projectileCount; i++)
            {
                float angle = start + i * step;
                float finalAngle = ApplyAngleModifiers(angle, i, projectileCount, globalAngleOffsetDeg);
                Vector2 dir = DirFromAngle(finalAngle);
                Spawn(shooter, origin, dir);
            }
        }
    }
} 
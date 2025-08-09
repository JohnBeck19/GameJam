using UnityEngine;

namespace GameJam.Combat
{
    [CreateAssetMenu(menuName = "Combat/Shot Patterns/Single Shot", fileName = "SingleShotPattern")]
    public class SingleShotPattern : ShotPattern
    {
        public enum Axis
        {
            Right,
            Up
        }

        [Header("Aim")]
        public bool useFirePointRotation = true;
        public Axis axis = Axis.Right;
        public Vector2 fixedDirection = Vector2.right; // used if not using firePoint rotation

        public override void Fire(Transform shooter, Transform firePoint)
        {
            FireWithAngleOffset(shooter, firePoint, 0f);
        }

        public override void FireWithAngleOffset(Transform shooter, Transform firePoint, float globalAngleOffsetDeg)
        {
            Vector2 baseDir;
            if (useFirePointRotation && firePoint != null)
            {
                baseDir = axis == Axis.Right ? (Vector2)firePoint.right : (Vector2)firePoint.up;
            }
            else
            {
                baseDir = fixedDirection.sqrMagnitude > 0.000001f ? fixedDirection.normalized : Vector2.right;
            }

            float baseAngle = AngleFromDirection(baseDir);
            float finalAngle = ApplyAngleModifiers(baseAngle, 0, 1, globalAngleOffsetDeg);
            Vector2 dir = DirFromAngle(finalAngle);
            Spawn(shooter, firePoint != null ? firePoint : shooter, dir);
        }
    }
} 
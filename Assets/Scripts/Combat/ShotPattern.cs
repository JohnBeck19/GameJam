using UnityEngine;

namespace GameJam.Combat
{
    public abstract class ShotPattern : ScriptableObject
    {
        [Header("Projectile")]
        public Projectile2D projectilePrefab;
        public ProjectileTrajectory trajectory;
        public float projectileSpeed = 10f;
        public ProjectileAttachmentMode attachmentMode = ProjectileAttachmentMode.Detached;

        [Header("Angle Modifiers")]
        [Tooltip("If enabled, applies a random angular deviation within ±Cone Half Angle to each projectile.")]
        public bool enableAngleCone = false;
        [Tooltip("Half-angle in degrees for the cone spread. 0 = no spread.")]
        public float coneHalfAngleDeg = 0f;
        [Tooltip("If true, jitter is applied uniquely per projectile. If false, one jitter per Fire call.")]
        public bool jitterPerProjectile = true;

        // Internal spawn collection used to return references for callers that need to manage spawned projectiles
        private static bool s_isCollectingSpawns;
        private static System.Collections.Generic.List<Projectile2D> s_spawnBuffer;

        /// <summary>
        /// Fire the pattern from the given shooter and fire point.
        /// </summary>
        public abstract void Fire(Transform shooter, Transform firePoint);

        /// <summary>
        /// Fire and return all spawned projectiles for this call.
        /// </summary>
        public System.Collections.Generic.List<Projectile2D> FireAndReturnSpawns(Transform shooter, Transform firePoint)
        {
            if (s_spawnBuffer == null) s_spawnBuffer = new System.Collections.Generic.List<Projectile2D>(8);
            s_spawnBuffer.Clear();
            s_isCollectingSpawns = true;
            Fire(shooter, firePoint);
            s_isCollectingSpawns = false;
            return new System.Collections.Generic.List<Projectile2D>(s_spawnBuffer);
        }

        /// <summary>
        /// Optional hook to use a global angle offset (degrees) provided by the caller.
        /// Default calls Fire(). Patterns can override to incorporate the offset.
        /// </summary>
        public virtual void FireWithAngleOffset(Transform shooter, Transform firePoint, float globalAngleOffsetDeg)
        {
            Fire(shooter, firePoint);
        }

        /// <summary>
        /// Fire with angle offset and return spawned projectiles for this call.
        /// If the concrete pattern doesn't use the offset override, it will fallback to Fire().
        /// </summary>
        public System.Collections.Generic.List<Projectile2D> FireWithAngleOffsetAndReturnSpawns(Transform shooter, Transform firePoint, float globalAngleOffsetDeg)
        {
            if (s_spawnBuffer == null) s_spawnBuffer = new System.Collections.Generic.List<Projectile2D>(8);
            s_spawnBuffer.Clear();
            s_isCollectingSpawns = true;
            FireWithAngleOffset(shooter, firePoint, globalAngleOffsetDeg);
            s_isCollectingSpawns = false;
            return new System.Collections.Generic.List<Projectile2D>(s_spawnBuffer);
        }

        protected Projectile2D Spawn(Transform shooter, Transform firePoint, Vector2 dir)
        {
            if (projectilePrefab == null)
            {
                Debug.LogWarning($"[{name}] No projectilePrefab assigned on shot pattern.");
                return null;
            }
            if (trajectory == null)
            {
                Debug.LogWarning($"[{name}] No trajectory assigned on shot pattern.");
                return null;
            }

            Vector3 spawnPos = firePoint != null ? firePoint.position : shooter.position;
            Quaternion rot = Quaternion.AngleAxis(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, Vector3.forward);

            Projectile2D proj = Instantiate(projectilePrefab, spawnPos, rot);
            // Initialize with shooter so projectile can read collision masks and attachment
            proj.Initialize(shooter, spawnPos, dir, projectileSpeed, trajectory, attachmentMode);

            if (s_isCollectingSpawns)
            {
                if (s_spawnBuffer == null) s_spawnBuffer = new System.Collections.Generic.List<Projectile2D>(8);
                s_spawnBuffer.Add(proj);
            }
            return proj;
        }

        protected float ApplyAngleModifiers(float baseAngleDeg, int projectileIndex, int projectileCount, float globalAngleOffsetDeg)
        {
            float angle = baseAngleDeg + globalAngleOffsetDeg;
            if (!enableAngleCone || coneHalfAngleDeg <= 0f)
            {
                return angle;
            }

            if (jitterPerProjectile)
            {
                angle += Random.Range(-coneHalfAngleDeg, coneHalfAngleDeg);
            }
            else
            {
                // Same jitter for all projectiles in this volley
                angle += GetSharedJitter(projectileIndex, projectileCount);
            }
            return angle;
        }

        private static float s_sharedJitterCache;
        private static int s_sharedJitterFrame = -1;
        private float GetSharedJitter(int projectileIndex, int projectileCount)
        {
            int frame = Time.frameCount;
            if (frame != s_sharedJitterFrame)
            {
                s_sharedJitterFrame = frame;
                s_sharedJitterCache = Random.Range(-coneHalfAngleDeg, coneHalfAngleDeg);
            }
            return s_sharedJitterCache;
        }

        protected static Vector2 DirFromAngle(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        protected static float AngleFromDirection(Vector2 dir)
        {
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
    }
} 
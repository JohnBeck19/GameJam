using UnityEngine;

namespace GameJam.Combat
{
    public class Projectile2D : MonoBehaviour
    {
        [Header("Lifetime")]
        [SerializeField]
        private float lifetimeSeconds = 5f;

        [Header("Collision")]
        [SerializeField] private bool destroyOnEnvironmentHit = true;
        [SerializeField] private bool destroyOnPlayerHit = true;
        private LayerMask playerMask;
        private LayerMask environmentMask;

        [Header("Visual")]
        [SerializeField]
        private bool faceMovementDirection = true;

        [Header("Damage")]
        [SerializeField] private float baseDamage = 1f;
        private float damagePercent = 100f;

        private ProjectileTrajectory trajectory;
        private float speed;
        private float age;
        private bool attachedToShooter;
        private Vector2 initialDirSpace; // World if detached, Local if attached
        private Vector3 initialWorldPosition;
        private Vector3 initialLocalPosition;
        private Transform shooterParent;

        // Keep a persistent reference to the shooter to support return-to-shooter logic when detached
        private Transform shooterWorldRef;

        public void Initialize(Transform shooter, Vector3 spawnPosition, Vector2 initialDirection, float projectileSpeed, ProjectileTrajectory trajectoryAsset, ProjectileAttachmentMode attachmentMode)
        {
            // Pull collision masks from shooter if available
            if (shooter != null && shooter.TryGetComponent<IProjectileCollisionProvider>(out var provider))
            {
                playerMask = provider.PlayerCollisionMask;
                environmentMask = provider.EnvironmentCollisionMask;
            }
            // Pull damage percent if available
            if (shooter != null && shooter.TryGetComponent<IProjectileDamageProvider>(out var dmg))
            {
                damagePercent = Mathf.Max(0f, dmg.DamagePercent);
            }
            trajectory = trajectoryAsset;
            speed = projectileSpeed;
            attachedToShooter = attachmentMode == ProjectileAttachmentMode.ParentToShooter;
            age = 0f;

            shooterWorldRef = shooter;

            if (attachedToShooter && shooter != null)
            {
                // Parent to shooter, keep world position, but we will drive localPosition by trajectory
                transform.SetParent(shooter, true);
                transform.position = spawnPosition;
                shooterParent = shooter;
                initialLocalPosition = transform.localPosition;

                // Convert world direction to shooter's local space so motion remains correct as shooter rotates
                Vector3 local = shooter.InverseTransformDirection(new Vector3(initialDirection.x, initialDirection.y, 0f));
                initialDirSpace = new Vector2(local.x, local.y).normalized;
            }
            else
            {
                // Detached: no parent, drive world position by trajectory
                transform.SetParent(null, true);
                transform.position = spawnPosition;
                shooterParent = null;
                initialWorldPosition = transform.position;
                initialDirSpace = initialDirection.normalized;
            }

            // Face initial direction immediately if requested
            if (faceMovementDirection && initialDirSpace.sqrMagnitude > 0.000001f)
            {
                Vector2 worldDir = attachedToShooter && shooter != null
                    ? (Vector2)shooter.TransformDirection(new Vector3(initialDirSpace.x, initialDirSpace.y, 0f))
                    : initialDirSpace;

                float angleDeg = Mathf.Atan2(worldDir.y, worldDir.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angleDeg, Vector3.forward);
            }
        }

        private void Update()
        {
            if (trajectory == null)
            {
                // No trajectory assigned; destroy quickly to avoid leaks
                Destroy(gameObject);
                return;
            }

            age += Time.deltaTime;
            if (lifetimeSeconds > 0f && age >= lifetimeSeconds)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 disp = trajectory.EvaluateDisplacement(age, initialDirSpace, speed);

            if (attachedToShooter && shooterParent != null)
            {
                transform.localPosition = initialLocalPosition + (Vector3)disp;
            }
            else
            {
                // Default world-space displacement
                Vector3 targetWorldPos = initialWorldPosition + (Vector3)disp;

                // Special handling for boomerang homing back to shooter
                if (trajectory is BoomerangTrajectory boom && boom.returnToShooter && shooterWorldRef != null)
                {
                    float t = age;
                    if (t > boom.outwardDuration)
                    {
                        // During return phase, blend toward shooter's current position by using return curve as weight
                        float t2 = t - boom.outwardDuration;
                        float norm = boom.returnDuration > 0f ? Mathf.Clamp01(t2 / boom.returnDuration) : 1f;
                        float w = boom.returnCurve.Evaluate(norm); // 1 at start of return, 0 at end
                        Vector3 outwardApex = initialWorldPosition + (Vector3)(initialDirSpace.normalized * (speed * boom.outwardDuration));
                        // Lerp from outward apex toward shooter over the return using (1 - w) as progress
                        Vector3 homed = Vector3.Lerp(outwardApex, shooterWorldRef.position, 1f - w);
                        targetWorldPos = homed;

                        if (boom.destroyWhenReachingShooter && Vector3.Distance(targetWorldPos, shooterWorldRef.position) <= boom.returnCompleteRadius)
                        {
                            Destroy(gameObject);
                            return;
                        }
                    }
                }

                transform.position = targetWorldPos;
            }

            if (faceMovementDirection)
            {
                // Approximate instantaneous direction via derivative over small dt
                const float dt = 0.001f;
                float tPrev = Mathf.Max(0f, age - dt);
                Vector2 prev = trajectory.EvaluateDisplacement(tPrev, initialDirSpace, speed);
                Vector2 vel = (disp - prev) / (age - tPrev + 1e-6f);
                if (vel.sqrMagnitude > 0.000001f)
                {
                    Vector2 worldDir = vel;
                    if (attachedToShooter && shooterParent != null)
                    {
                        worldDir = shooterParent.TransformDirection(new Vector3(vel.x, vel.y, 0f));
                    }
                    float angleDeg = Mathf.Atan2(worldDir.y, worldDir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.AngleAxis(angleDeg, Vector3.forward);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            int otherLayer = other.gameObject.layer;

            // Compute damage value
            float totalDamage = baseDamage * (damagePercent / 100f);

            if (destroyOnEnvironmentHit && environmentMask != 0 && (environmentMask.value & (1 << otherLayer)) != 0)
            {
                // TODO: optionally notify environment of impact with damage if desired
                Destroy(gameObject);
                return;
            }

            if (destroyOnPlayerHit && playerMask != 0 && (playerMask.value & (1 << otherLayer)) != 0)
            {
                // TODO: apply damage to player when Health is implemented on player object
                Destroy(gameObject);
                return;
            }
        }
    }
} 
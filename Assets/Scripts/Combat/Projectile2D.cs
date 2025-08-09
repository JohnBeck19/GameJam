using UnityEngine;

namespace GameJam.Combat
{
    public class Projectile2D : MonoBehaviour
    {
        [Header("Lifetime")]
        [SerializeField]
        private float lifetimeSeconds = 5f;

        [Header("Visual")]
        [SerializeField]
        private bool faceMovementDirection = true;

        private ProjectileTrajectory trajectory;
        private float speed;
        private float age;
        private bool attachedToShooter;
        private Vector2 initialDirSpace; // World if detached, Local if attached
        private Vector3 initialWorldPosition;
        private Vector3 initialLocalPosition;
        private Transform shooterParent;

        public void Initialize(Transform shooter, Vector3 spawnPosition, Vector2 initialDirection, float projectileSpeed, ProjectileTrajectory trajectoryAsset, ProjectileAttachmentMode attachmentMode)
        {
            trajectory = trajectoryAsset;
            speed = projectileSpeed;
            attachedToShooter = attachmentMode == ProjectileAttachmentMode.ParentToShooter;
            age = 0f;

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
            if (age >= lifetimeSeconds)
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
                transform.position = initialWorldPosition + (Vector3)disp;
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
    }
} 
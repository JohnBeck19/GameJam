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

        [Header("Debug")]
        [Tooltip("If true, logs collisions for projectiles fired by the Player.")]
        [SerializeField] private bool debugPlayerBulletCollisions = true;

        // Cached components
        private Rigidbody2D _rb2d;
        private Collider2D _collider2d;

        public void Initialize(Transform shooter, Vector3 spawnPosition, Vector2 initialDirection, float projectileSpeed, ProjectileTrajectory trajectoryAsset, ProjectileAttachmentMode attachmentMode)
        {
            // Ensure required physics components exist for trigger callbacks
            _collider2d = GetComponent<Collider2D>();
            if (_collider2d == null)
            {
                _collider2d = gameObject.AddComponent<CircleCollider2D>();
                ((CircleCollider2D)_collider2d).radius = 0.1f;
            }
            _collider2d.isTrigger = true;

            _rb2d = GetComponent<Rigidbody2D>();
            if (_rb2d == null)
            {
                _rb2d = gameObject.AddComponent<Rigidbody2D>();
            }
            _rb2d.bodyType = RigidbodyType2D.Kinematic;
            _rb2d.gravityScale = 0f;
            _rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Pull collision masks from shooter if available
            if (shooter != null && shooter.TryGetComponent<IProjectileCollisionProvider>(out var provider))
            {
                playerMask = provider.PlayerCollisionMask;
                environmentMask = provider.EnvironmentCollisionMask;
            }
            else
            {
                playerMask = 0;
                environmentMask = 0;
            }
            // Sensible defaults if no target mask provided
            if (playerMask == 0)
            {
                bool shooterIsPlayer = shooter != null && shooter.GetComponent<Player>() != null;
                int fallback = LayerMask.NameToLayer(shooterIsPlayer ? "Enemy" : "Player");
                if (fallback < 0)
                {
                    // Try plural form too
                    fallback = LayerMask.NameToLayer(shooterIsPlayer ? "Enemies" : "Players");
                }
                if (fallback >= 0)
                {
                    playerMask = 1 << fallback;
                }
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
            bool shooterIsPlayer = shooterWorldRef != null && shooterWorldRef.GetComponent<Player>() != null;

            // Compute damage value
            float totalDamage = baseDamage * (damagePercent / 100f);

            if (destroyOnEnvironmentHit && environmentMask != 0 && (environmentMask.value & (1 << otherLayer)) != 0)
            {
                if (shooterIsPlayer && debugPlayerBulletCollisions)
                {
                    Debug.Log($"[Projectile] Player bullet hit environment '{other.name}' on layer '{LayerMask.LayerToName(otherLayer)}'. Destroying projectile.");
                }
                // TODO: optionally notify environment of impact with damage if desired
                Destroy(gameObject);
                return;
            }

            // Target hit handling using the provider's target mask (named playerMask here for backward-compat)
            bool hitInTargetMask = playerMask != 0 && (playerMask.value & (1 << otherLayer)) != 0;
            // Additional safety: if shooter is player and collided layer equals Enemy/Enemies, treat as in mask
            if (!hitInTargetMask && shooterIsPlayer)
            {
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                int enemiesLayer = LayerMask.NameToLayer("Enemies");
                if ((enemyLayer >= 0 && otherLayer == enemyLayer) || (enemiesLayer >= 0 && otherLayer == enemiesLayer))
                {
                    hitInTargetMask = true;
                }
            }
            if (shooterIsPlayer && debugPlayerBulletCollisions)
            {
                Debug.Log($"[Projectile] Player bullet collided with '{other.name}' (layer '{LayerMask.LayerToName(otherLayer)}'), inTargetMask={hitInTargetMask}.");
            }
            if (hitInTargetMask)
            {
                if (shooterIsPlayer)
                {
                    // Player projectile -> damage enemies
                    var enemyHealth = other.GetComponentInParent<GameJam.Enemies.EnemyHealth>();
                    if (enemyHealth != null)
                    {
                        if (debugPlayerBulletCollisions)
                        {
                            Debug.Log($"[Projectile] Damaging EnemyHealth '{enemyHealth.gameObject.name}' for {totalDamage:F1}.");
                        }
                        enemyHealth.TakeDamage(totalDamage);
                        if (destroyOnPlayerHit)
                            Destroy(gameObject);
                        return;
                    }
                    var enemy = other.GetComponentInParent<GameJam.Enemies.EnemyAgent2D>();
                    if (enemy != null)
                    {
                        if (debugPlayerBulletCollisions)
                        {
                            Debug.Log($"[Projectile] No EnemyHealth found; destroying enemy '{enemy.gameObject.name}'.");
                        }
                        Destroy(enemy.gameObject);
                        if (destroyOnPlayerHit)
                            Destroy(gameObject);
                        return;
                    }
                    if (debugPlayerBulletCollisions)
                    {
                        Debug.Log($"[Projectile] Hit target in mask but found no enemy components. No action taken.");
                    }
                }
                else
                {
                    // Non-player projectile -> attempt to damage player
                    if (Player.Instance != null)
                    {
                        // Ensure we actually hit the player hierarchy
                        var hitPlayer = other.GetComponentInParent<Player>();
                        if (hitPlayer != null)
                        {
                            Player.Instance.TakeDmg(totalDamage);
                            if (destroyOnPlayerHit)
                                Destroy(gameObject);
                            return;
                        }
                    }
                }
            }
            else if (shooterIsPlayer && debugPlayerBulletCollisions)
            {
                Debug.Log($"[Projectile] Player bullet collision ignored: '{other.name}' not in target mask.");
            }
        }
    }
} 
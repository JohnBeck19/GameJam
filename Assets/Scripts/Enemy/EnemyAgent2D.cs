using UnityEngine;
using GameJam.Combat;

namespace GameJam.Enemies
{
    [DisallowMultipleComponent]
    public class EnemyAgent2D : MonoBehaviour, IProjectileCollisionProvider, IProjectileDamageProvider
    {
        [Header("Targeting")]
        [SerializeField] private float detectionRadius = 8f;
        [SerializeField] private LayerMask playerLayerMask;

        [Header("Attacking")]
        [Tooltip("Enemies will only fire when the target is within this range. Defaults to detection radius if <= 0.")]
        [SerializeField] private float attackRange = 0f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float facingAngleOffset = 0f;

        [Header("Behavior")]
        [SerializeField] private EnemyBehaviorPattern behaviorPattern;
        [SerializeField] private ShotPattern[] attackPatterns;
        [SerializeField] private Transform firePoint; // optional

        [Header("Projectile Collision Masks")]
        [SerializeField] private LayerMask playerCollisionMask;
        [SerializeField] private LayerMask environmentCollisionMask;

        [Header("Damage")]
        [SerializeField, Range(0f, 1000f)] private float damagePercent = 100f;

        private EnemyBehaviorState runtimeState;
        private Transform currentTarget;

        private void Awake()
        {
            runtimeState = behaviorPattern != null ? behaviorPattern.CreateState() : null;
            if (playerLayerMask == 0)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0)
                {
                    playerLayerMask = 1 << playerLayer;
                }
            }
        }

        private void Update()
        {
            AcquireTarget();
            if (behaviorPattern != null)
            {
                behaviorPattern.Tick(this, runtimeState, Time.deltaTime);
            }
        }

        public void MoveTowards(Vector2 worldTarget, float maxStep)
        {
            Vector3 pos = transform.position;
            Vector2 delta = (Vector2)worldTarget - (Vector2)pos;
            float dist = delta.magnitude;
            if (dist <= 0.0001f) return;
            float step = Mathf.Min(maxStep, dist);
            Vector2 dir = delta / dist;
            transform.position = pos + (Vector3)(dir * step);

            // Face
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + facingAngleOffset;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        public void MaintainSpacing(Vector2 worldTarget, float desiredDistance, float maxStep)
        {
            Vector2 pos = transform.position;
            Vector2 toTarget = worldTarget - pos;
            float dist = toTarget.magnitude;
            if (dist <= 0.0001f) return;
            float error = dist - desiredDistance;
            if (Mathf.Abs(error) < 0.01f) return;
            Vector2 dir = toTarget.normalized * Mathf.Sign(error);
            float step = Mathf.Min(Mathf.Abs(error), maxStep);
            transform.position = (Vector2)transform.position + dir * step;
        }

        public void FireAllPatterns()
        {
            if (attackPatterns == null) return;

            // Only shoot if we have a target and it's within attack range
            if (currentTarget == null) return;
            float range = attackRange > 0f ? attackRange : detectionRadius;
            if (((currentTarget.position - transform.position).sqrMagnitude) > (range * range))
            {
                return;
            }
            foreach (var p in attackPatterns)
            {
                if (p == null) continue;
                p.FireWithAngleOffset(transform, firePoint != null ? firePoint : transform, 0f);
            }
        }

        public Transform GetTarget() => currentTarget;
        public float GetMoveSpeed() => moveSpeed;
        public Transform GetFirePointOrSelf() => firePoint != null ? firePoint : transform;
        public float GetAttackRange() => attackRange > 0f ? attackRange : detectionRadius;

        private void AcquireTarget()
        {
            if (playerLayerMask == 0)
                return;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, playerLayerMask);
            if (hits == null || hits.Length == 0)
            {
                currentTarget = null;
                return;
            }
            // Nearest
            float best = float.PositiveInfinity;
            Transform nearest = null;
            Vector3 self = transform.position;
            foreach (var h in hits)
            {
                float sq = (h.transform.position - self).sqrMagnitude;
                if (sq < best)
                {
                    best = sq; nearest = h.transform;
                }
            }
            currentTarget = nearest;
        }

        // IProjectileCollisionProvider
        public LayerMask PlayerCollisionMask => playerCollisionMask;
        public LayerMask EnvironmentCollisionMask => environmentCollisionMask;

        // IProjectileDamageProvider
        public float DamagePercent => damagePercent;
        public void SetDamagePercent(float percent) { damagePercent = Mathf.Max(0f, percent); }
    }
} 
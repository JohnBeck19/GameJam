using UnityEngine;
using GameJam.Combat;

namespace GameJam.Enemies
{
    public class ChaserEnemy2D : MonoBehaviour, IProjectileCollisionProvider
    {
        [Header("Detection")]
        [SerializeField]
        private float detectionRadius = 6f;

        [Tooltip("Layer(s) that represent player objects. Defaults to the 'Player' layer if left empty at runtime.")]
        [SerializeField]
        private LayerMask playerLayerMask;

        [Header("Movement")]
        [SerializeField]
        private float moveSpeed = 3f;
        
        [Tooltip("How far to stop before touching the target's collider surface.")]
        [SerializeField]
        private float approachBuffer = 0.1f;

        [Tooltip("Used if the target has no Collider2D. Stop this far from its transform position.")]
        [SerializeField]
        private float stopDistanceFallback = 0.5f;

        [Tooltip("Angle offset (in degrees) applied when rotating to face movement. Use this if your sprite's forward direction isn't to the right (0°). For up-facing sprites, try -90.")]
        [SerializeField]
        private float facingAngleOffset = 0f;

        [Header("Projectile Collision Masks")]
        [SerializeField] private LayerMask playerCollisionMask;
        [SerializeField] private LayerMask environmentCollisionMask;

        private Transform currentTarget;
        private Collider2D currentTargetCollider;
        private Collider2D selfCollider;

        private void Awake()
        {
            // If not set in inspector, use the 'Player' layer by name if it exists
            if (playerLayerMask == 0)
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0)
                {
                    playerLayerMask = 1 << playerLayer;
                }
            }

            selfCollider = GetComponent<Collider2D>();
        }

        private void Update()
        {
            AcquireTargetIfNeeded();
            ChaseTargetIfAny();
        }

        private void AcquireTargetIfNeeded()
        {
            if (currentTarget != null)
            {
                float distance = Vector2.Distance(transform.position, currentTarget.position);
                if (distance <= detectionRadius && currentTarget.gameObject.activeInHierarchy)
                {
                    return; // Keep current target if still valid and in range
                }

                currentTarget = null;
                currentTargetCollider = null;
            }

            if (playerLayerMask == 0)
            {
                return; // No valid layer to search
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, playerLayerMask);
            if (hits == null || hits.Length == 0)
            {
                currentTarget = null;
                currentTargetCollider = null;
                return;
            }

            // Pick nearest target
            float nearestSqr = float.PositiveInfinity;
            Transform nearest = null;
            Vector3 selfPos = transform.position;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform candidate = hits[i].transform;
                float sqr = (candidate.position - selfPos).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = candidate;
                }
            }

            currentTarget = nearest;
            currentTargetCollider = FindAnyColliderOnTransform(nearest);
        }

        private void ChaseTargetIfAny()
        {
            if (currentTarget == null)
            {
                return;
            }

            Vector3 selfPos3 = transform.position;
            float step = moveSpeed * Time.deltaTime;
            Vector2 moveDirection;
            float moveDistance;

            if (currentTargetCollider != null)
            {
                Vector2 closestOnTarget = currentTargetCollider.ClosestPoint(selfPos3);
                Vector2 toSurface = closestOnTarget - (Vector2)selfPos3;
                float distToSurface = toSurface.magnitude;

                if (distToSurface <= Mathf.Max(approachBuffer, 0f))
                {
                    // Already at/inside buffer range; just face target
                    moveDirection = toSurface.sqrMagnitude > 0.000001f ? toSurface.normalized : ((Vector2)(currentTarget.position - selfPos3)).normalized;
                    moveDistance = 0f;
                }
                else
                {
                    moveDirection = toSurface.normalized;
                    float maxAllowed = Mathf.Max(0f, distToSurface - approachBuffer);
                    moveDistance = Mathf.Min(step, maxAllowed);
                }
            }
            else
            {
                // Fallback if the player has no collider
                Vector2 toTarget = (Vector2)(currentTarget.position - selfPos3);
                float dist = toTarget.magnitude;
                if (dist <= stopDistanceFallback)
                {
                    moveDirection = toTarget.sqrMagnitude > 0.000001f ? toTarget.normalized : Vector2.zero;
                    moveDistance = 0f;
                }
                else
                {
                    moveDirection = toTarget.normalized;
                    float maxAllowed = Mathf.Max(0f, dist - stopDistanceFallback);
                    moveDistance = Mathf.Min(step, maxAllowed);
                }
            }

            if (moveDistance > 0f)
            {
                transform.position += (Vector3)(moveDirection * moveDistance);
            }

            // Look/face toward the movement/target
            Vector2 facingDir = moveDirection;
            if (facingDir.sqrMagnitude < 0.000001f)
            {
                facingDir = (currentTarget.position - transform.position).normalized;
            }

            if (facingDir.sqrMagnitude > 0.000001f)
            {
                float angleDeg = Mathf.Atan2(facingDir.y, facingDir.x) * Mathf.Rad2Deg + facingAngleOffset;
                transform.rotation = Quaternion.AngleAxis(angleDeg, Vector3.forward);
            }
        }

        private static Collider2D FindAnyColliderOnTransform(Transform t)
        {
            if (t == null) return null;
            return t.GetComponent<Collider2D>() ?? t.GetComponentInChildren<Collider2D>() ?? t.GetComponentInParent<Collider2D>();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }

        // IProjectileCollisionProvider implementation
        public LayerMask PlayerCollisionMask => playerCollisionMask;
        public LayerMask EnvironmentCollisionMask => environmentCollisionMask;
    }
} 
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameJam.Combat
{
    [DisallowMultipleComponent]
    public class PlayerShooter : MonoBehaviour, IProjectileCollisionProvider, IProjectileDamageProvider
    {
        [Header("Shot Patterns")]
        [Tooltip("Volleys that fire on an interval.")]
        [SerializeField] private ShotPattern[] activePatterns;

        [Tooltip("Patterns that should persist (e.g., satellites). These will be spawned once on enable and not auto-despawned by intervals.")]
        [SerializeField] private ShotPattern[] persistentPatterns;

        [Header("Firing Settings")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private bool autoFire = true;
        [SerializeField] private float fireIntervalSeconds = 0.25f;
        [Tooltip("Multiplier applied to fire rate. 1 = base. 2 = twice as fast (half the interval). 0.5 = half as fast.")]
        [SerializeField] private float fireRateMultiplier = 1f;

        [Header("Input")]
        [Tooltip("If true, requires input to fire. Holding input continues to fire at interval; clicking fires immediately once.")]
        [SerializeField] private bool requireInputToFire = false;
        [Tooltip("New Input System action name to read for firing (WasPressed/IsPressed).")]
        [SerializeField] private string fireActionName = "Fire";
        [Tooltip("Legacy fallback input when New Input System isn't present.")]
        [SerializeField] private KeyCode legacyFireKey = KeyCode.Mouse0;

        [Header("Persistent Cleanup")]
        [SerializeField] private bool autoDespawnPersistentOnDisable = true;

        [Header("Damage")]
        [Tooltip("Base damage multiplier percent. 100 = normal. Can be modified at runtime by powerups.")]
        [SerializeField, Range(0f, 1000f)] private float damagePercent = 100f;

        [Header("Projectile Collision Masks")]
        [SerializeField] private LayerMask playerCollisionMask;
        [SerializeField] private LayerMask environmentCollisionMask;

        private float timer;
        private PlayerInput playerInput;
#if ENABLE_INPUT_SYSTEM
        private InputAction fireAction;
#endif
        // Cooldown-based gating to enforce max fire rate regardless of input spam
        private float cooldownRemaining;
        private readonly List<Projectile2D> persistentSpawned = new List<Projectile2D>();

        public float DamagePercent => damagePercent;
        public void SetDamagePercent(float percent) { damagePercent = Mathf.Max(0f, percent); }

        private void Reset()
        {
            firePoint = transform;
        }

        private void OnEnable()
        {
            SpawnPersistent();
            SetupInput();
            cooldownRemaining = 0f;
        }

        private void OnDisable()
        {
            if (autoDespawnPersistentOnDisable)
            {
                DespawnPersistent();
            }
        }

        private void Update()
        {
            if (activePatterns == null || activePatterns.Length == 0)
                return; // No active patterns => no shooting

            float dt = Time.deltaTime;
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= dt;
                if (cooldownRemaining < 0f) cooldownRemaining = 0f;
            }

            if (requireInputToFire)
            {
                bool wantsToFire = GetFirePressedThisFrame() || GetFireHeld();
                if (wantsToFire && cooldownRemaining <= 0f)
                {
                    FireActiveOnce();
                    cooldownRemaining = GetEffectiveInterval();
                }
                return;
            }

            if (!autoFire)
                return;

            if (cooldownRemaining <= 0f)
            {
                FireActiveOnce();
                cooldownRemaining = GetEffectiveInterval();
            }
        }

        public void FireActiveOnce()
        {
            if (activePatterns == null) return;
            Transform origin = firePoint != null ? firePoint : transform;
            foreach (var p in activePatterns)
            {
                if (p == null) continue;
                p.FireWithAngleOffset(transform, origin, 0f);
            }
        }

        public void SetActivePatterns(params ShotPattern[] patterns)
        {
            activePatterns = patterns;
        }

        public void SetPersistentPatterns(params ShotPattern[] patterns)
        {
            persistentPatterns = patterns;
            SpawnPersistent(true);
        }

        public void DespawnPersistent()
        {
            for (int i = 0; i < persistentSpawned.Count; i++)
            {
                var proj = persistentSpawned[i];
                if (proj != null)
                {
                    Destroy(proj.gameObject);
                }
            }
            persistentSpawned.Clear();
        }

        private void SpawnPersistent(bool clearExisting = false)
        {
            if (clearExisting)
            {
                DespawnPersistent();
            }

            if (persistentPatterns == null || persistentPatterns.Length == 0) return;

            Transform origin = firePoint != null ? firePoint : transform;
            foreach (var p in persistentPatterns)
            {
                if (p == null) continue;
                // For persistent effects like satellites, patterns should use attachment or long/infinite lifetime.
                var spawned = p.FireWithAngleOffsetAndReturnSpawns(transform, origin, 0f);
                if (spawned != null)
                {
                    for (int i = 0; i < spawned.Count; i++)
                    {
                        var proj = spawned[i];
                        if (proj != null) persistentSpawned.Add(proj);
                    }
                }
            }
        }

        // IProjectileCollisionProvider
        public LayerMask PlayerCollisionMask => playerCollisionMask;
        public LayerMask EnvironmentCollisionMask => environmentCollisionMask;

        // Runtime configuration helpers
        public void SetInterval(float seconds)
        {
            fireIntervalSeconds = Mathf.Max(0.01f, seconds);
        }

        public void SetShotsPerSecond(float shotsPerSecond)
        {
            float sps = Mathf.Max(0.01f, shotsPerSecond);
            fireIntervalSeconds = 1f / sps;
        }

        public void SetFirePoint(Transform point)
        {
            firePoint = point;
        }

        public void SetAutofire(bool enabled)
        {
            autoFire = enabled;
        }

        public void SetRequireInputToFire(bool required)
        {
            requireInputToFire = required;
        }

        public void SetFireRateMultiplier(float multiplier)
        {
            fireRateMultiplier = Mathf.Max(0.01f, multiplier);
        }

        public float GetEffectiveInterval()
        {
            float baseInterval = Mathf.Max(0.01f, fireIntervalSeconds);
            float mult = Mathf.Max(0.01f, fireRateMultiplier);
            return baseInterval / mult;
        }

        private void SetupInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
                if (playerInput == null)
                {
                    playerInput = GetComponentInParent<PlayerInput>();
                }
            }
            if (playerInput != null && !string.IsNullOrEmpty(fireActionName))
            {
                fireAction = playerInput.actions != null ? playerInput.actions[fireActionName] : null;
            }
#endif
        }

        private bool GetFirePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (fireAction == null && playerInput != null && playerInput.actions != null && !string.IsNullOrEmpty(fireActionName))
            {
                fireAction = playerInput.actions[fireActionName];
            }
            if (fireAction != null)
            {
                return fireAction.WasPressedThisFrame();
            }
#endif
            return Input.GetKeyDown(legacyFireKey) || Input.GetMouseButtonDown(0);
        }

        private bool GetFireHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (fireAction == null && playerInput != null && playerInput.actions != null && !string.IsNullOrEmpty(fireActionName))
            {
                fireAction = playerInput.actions[fireActionName];
            }
            if (fireAction != null)
            {
                return fireAction.IsPressed();
            }
#endif
            return Input.GetKey(legacyFireKey) || Input.GetMouseButton(0);
        }
    }
} 
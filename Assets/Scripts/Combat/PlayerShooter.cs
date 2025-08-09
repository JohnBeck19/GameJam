using System.Collections.Generic;
using UnityEngine;

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

        [Header("Persistent Cleanup")]
        [SerializeField] private bool autoDespawnPersistentOnDisable = true;

        [Header("Damage")]
        [Tooltip("Base damage multiplier percent. 100 = normal. Can be modified at runtime by powerups.")]
        [SerializeField, Range(0f, 1000f)] private float damagePercent = 100f;

        [Header("Projectile Collision Masks")]
        [SerializeField] private LayerMask playerCollisionMask;
        [SerializeField] private LayerMask environmentCollisionMask;

        private float timer;
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
            if (!autoFire)
                return;

            if (activePatterns == null || activePatterns.Length == 0)
                return; // No active patterns => no shooting

            timer += Time.deltaTime;
            if (timer >= Mathf.Max(0.01f, fireIntervalSeconds))
            {
                timer = 0f;
                FireActiveOnce();
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
    }
} 
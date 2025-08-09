using UnityEngine;
using GameJam.Combat;

namespace GameJam.Enemies
{
    [DisallowMultipleComponent]
    public class EnemyCollisionProvider2D : MonoBehaviour, IProjectileCollisionProvider
    {
        [Header("Projectile Collision Masks")]
        [SerializeField]
        private LayerMask playerCollisionMask;
        [SerializeField]
        private LayerMask environmentCollisionMask;

        public LayerMask PlayerCollisionMask => playerCollisionMask;
        public LayerMask EnvironmentCollisionMask => environmentCollisionMask;
    }
} 
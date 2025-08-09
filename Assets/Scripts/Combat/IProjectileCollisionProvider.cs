using UnityEngine;

namespace GameJam.Combat
{
    public interface IProjectileCollisionProvider
    {
        LayerMask PlayerCollisionMask { get; }
        LayerMask EnvironmentCollisionMask { get; }
    }
} 
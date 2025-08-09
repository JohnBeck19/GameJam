using UnityEngine;

namespace GameJam.Combat
{
    public interface IProjectileDamageProvider
    {
        // 100 = base damage, can be higher/lower at runtime
        float DamagePercent { get; }
    }
} 
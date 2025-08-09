using UnityEngine;

namespace GameJam.Combat
{
    [CreateAssetMenu(menuName = "Combat/Trajectories/Linear", fileName = "LinearTrajectory")]
    public class LinearTrajectory : ProjectileTrajectory
    {
        public override Vector2 EvaluateDisplacement(float timeSeconds, Vector2 initialDirectionNormalized, float speedUnitsPerSecond)
        {
            if (initialDirectionNormalized.sqrMagnitude > 0.000001f)
            {
                initialDirectionNormalized.Normalize();
            }
            return initialDirectionNormalized * speedUnitsPerSecond * Mathf.Max(0f, timeSeconds);
        }
    }
} 
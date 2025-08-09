using UnityEngine;

namespace GameJam.Combat
{
    [CreateAssetMenu(menuName = "Combat/Trajectories/Boomerang", fileName = "BoomerangTrajectory")]
    public class BoomerangTrajectory : ProjectileTrajectory
    {
        [Header("Boomerang")]
        public float outwardDuration = 0.6f;
        public float returnDuration = 0.6f;
        public AnimationCurve outwardCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Return Behavior")]
        [Tooltip("If true, during the return phase the boomerang homes back to the shooter (enemy or player) that fired it.")]
        public bool returnToShooter = true;
        [Tooltip("If true, end lifetime as soon as the boomerang reaches the shooter on the return.")]
        public bool destroyWhenReachingShooter = true;
        [Tooltip("Distance to shooter at which the return is considered complete (used if destroyWhenReachingShooter is true).")]
        public float returnCompleteRadius = 0.2f;

        public override Vector2 EvaluateDisplacement(float timeSeconds, Vector2 initialDirectionNormalized, float speedUnitsPerSecond)
        {
            Vector2 dir = initialDirectionNormalized.sqrMagnitude > 0.000001f ? initialDirectionNormalized.normalized : Vector2.right;

            if (outwardDuration <= 0f && returnDuration <= 0f)
            {
                return Vector2.zero;
            }

            float t = timeSeconds;
            if (t <= outwardDuration)
            {
                float norm = outwardDuration > 0f ? Mathf.Clamp01(t / outwardDuration) : 1f;
                float dist = outwardCurve.Evaluate(norm) * speedUnitsPerSecond * outwardDuration;
                return dir * dist;
            }
            else
            {
                float t2 = t - outwardDuration;
                float norm = returnDuration > 0f ? Mathf.Clamp01(t2 / returnDuration) : 1f;
                float totalOut = speedUnitsPerSecond * outwardDuration; // maximum outward distance at end of phase
                float dist = returnCurve.Evaluate(norm) * totalOut;
                return dir * dist;
            }
        }
    }
} 
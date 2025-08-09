using UnityEngine;

namespace GameJam.Combat
{
    public abstract class ProjectileTrajectory : ScriptableObject
    {
        /// <summary>
        /// Returns the displacement (in units) since spawn, at timeSeconds, given an initial direction and speed.
        /// </summary>
        public abstract Vector2 EvaluateDisplacement(float timeSeconds, Vector2 initialDirectionNormalized, float speedUnitsPerSecond);
    }
} 
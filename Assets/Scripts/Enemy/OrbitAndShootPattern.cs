using UnityEngine;

namespace GameJam.Enemies
{
    [CreateAssetMenu(menuName = "Enemies/Behavior Patterns/Orbit And Shoot", fileName = "OrbitAndShootPattern")]
    public class OrbitAndShootPattern : EnemyBehaviorPattern
    {
        [Header("Orbit")]
        public float orbitRadius = 3f;
        public float angularSpeedDegPerSec = 60f;

        [Header("Fire")]
        public float fireInterval = 1.25f;

        public override EnemyBehaviorState CreateState()
        {
            return new State();
        }

        public override void Tick(EnemyAgent2D agent, EnemyBehaviorState state, float deltaTime)
        {
            var s = (State)state;
            s.angleDeg += angularSpeedDegPerSec * deltaTime;

            var target = agent.GetTarget();
            if (target != null)
            {
                Vector2 center = target.position;
                float rad = s.angleDeg * Mathf.Deg2Rad;
                Vector2 desired = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbitRadius;
                agent.MoveTowards(desired, agent.GetMoveSpeed() * deltaTime);

                s.fireTimer += deltaTime;
                if (s.fireTimer >= Mathf.Max(0.05f, fireInterval))
                {
                    s.fireTimer = 0f;
                    agent.FireAllPatterns();
                }
            }
        }

        private class State : EnemyBehaviorState
        {
            public float angleDeg;
            public float fireTimer;
        }
    }
} 
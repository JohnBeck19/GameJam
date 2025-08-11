using UnityEngine;

namespace GameJam.Enemies
{
    [CreateAssetMenu(menuName = "Enemies/Behavior Patterns/Chase And Shoot", fileName = "ChaseAndShootPattern")]
    public class ChaseAndShootPattern : EnemyBehaviorPattern
    {
        [Header("Chase")]
        public float desiredDistance = 1.5f;
        public float maxMoveSpeed = 3f;

        [Header("Fire")]
        public float fireInterval = 1.0f;
        public float warmupDelay = 0f;

        public override EnemyBehaviorState CreateState()
        {
            return new State();
        }

        public override void Tick(EnemyAgent2D agent, EnemyBehaviorState state, float deltaTime)
        {
            var s = (State)state;
            s.timer += deltaTime;

            var target = agent.GetTarget();
            if (target != null)
            {
                agent.MaintainSpacing(target.position, desiredDistance, maxMoveSpeed * deltaTime);
            }

            if (s.timer >= Mathf.Max(0.01f, warmupDelay))
            {
                s.fireTimer += deltaTime;
                if (s.fireTimer >= Mathf.Max(0.05f, fireInterval))
                {
                    s.fireTimer = 0f;
                    // Only fire if target is within attack range
                    if (target != null)
                    {
                        float range = agent.GetAttackRange();
                        if (((target.position - agent.transform.position).sqrMagnitude) <= (range * range))
                        {
                            agent.FireAllPatterns();
                        }
                    }
                }
            }
        }

        private class State : EnemyBehaviorState
        {
            public float timer;
            public float fireTimer;
        }
    }
} 
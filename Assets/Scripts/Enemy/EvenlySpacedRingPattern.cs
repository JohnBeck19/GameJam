using UnityEngine;

namespace GameJam.Enemies
{
    [CreateAssetMenu(menuName = "Enemies/Behavior Patterns/Evenly Spaced Ring", fileName = "EvenlySpacedRingPattern")]
    public class EvenlySpacedRingPattern : EnemyBehaviorPattern
    {
        [Header("Ring")]
        public float ringRadius = 4f;
        public float repositionSpeed = 4f;
        public float slotRecomputeInterval = 0.5f;

        [Header("Fire")]
        public float fireInterval = 1.5f;

        public override EnemyBehaviorState CreateState()
        {
            return new State();
        }

        public override void Tick(EnemyAgent2D agent, EnemyBehaviorState state, float deltaTime)
        {
            var s = (State)state;
            s.recomputeTimer += deltaTime;
            s.fireTimer += deltaTime;

            var target = agent.GetTarget();
            if (target == null) return;

            if (s.recomputeTimer >= Mathf.Max(0.05f, slotRecomputeInterval) || s.cachedSlotCount <= 0)
            {
                s.recomputeTimer = 0f;
                var agents = Object.FindObjectsOfType<EnemyAgent2D>();
                int count = 0;
                foreach (var a in agents) if (a != null && a != agent) count++;
                s.cachedSlotCount = Mathf.Max(1, count + 1);
                System.Array.Sort(agents, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
                int idx = System.Array.IndexOf(agents, agent);
                s.slotIndex = idx >= 0 ? idx : 0;
            }

            float angle = (360f / s.cachedSlotCount) * s.slotIndex;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 desired = (Vector2)target.position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * ringRadius;
            agent.MoveTowards(desired, repositionSpeed * deltaTime);

            if (s.fireTimer >= Mathf.Max(0.05f, fireInterval))
            {
                s.fireTimer = 0f;
                agent.FireAllPatterns();
            }
        }

        private class State : EnemyBehaviorState
        {
            public float recomputeTimer;
            public int cachedSlotCount;
            public int slotIndex;
            public float fireTimer;
        }
    }
} 
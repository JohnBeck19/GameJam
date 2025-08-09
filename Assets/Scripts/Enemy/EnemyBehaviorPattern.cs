using UnityEngine;

namespace GameJam.Enemies
{
    public abstract class EnemyBehaviorPattern : ScriptableObject
    {
        public abstract EnemyBehaviorState CreateState();
        public abstract void Tick(EnemyAgent2D agent, EnemyBehaviorState state, float deltaTime);
    }

    public abstract class EnemyBehaviorState { }
} 
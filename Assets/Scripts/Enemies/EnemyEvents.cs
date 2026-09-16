using UnityEngine;
using ArenaSurvivor.Core.Events;

namespace ArenaSurvivor.Enemies
{
    /// <summary>Se publica cuando un enemigo muere: quién escucha decide qué hacer (XP, score, etc.).</summary>
    public readonly struct EnemyKilledEvent : IGameEvent
    {
        public readonly Vector2 Position;
        public readonly int ExperienceReward;
        public readonly Color Color;
        public readonly bool DropsChest;

        public EnemyKilledEvent(Vector2 position, int experienceReward, Color color, bool dropsChest)
        {
            Position = position;
            ExperienceReward = experienceReward;
            Color = color;
            DropsChest = dropsChest;
        }
    }
}

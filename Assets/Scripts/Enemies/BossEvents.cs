using ArenaSurvivor.Core.Events;

namespace ArenaSurvivor.Enemies
{
    /// <summary>Se publica cuando aparece un jefe — el HUD muestra su barra de vida.</summary>
    public readonly struct BossSpawnedEvent : IGameEvent
    {
        public readonly string DisplayName;
        public readonly float MaxHealth;

        public BossSpawnedEvent(string displayName, float maxHealth)
        {
            DisplayName = displayName;
            MaxHealth = maxHealth;
        }
    }

    /// <summary>Se publica cada vez que cambia la vida del jefe actualmente en pantalla.</summary>
    public readonly struct BossHealthChangedEvent : IGameEvent
    {
        public readonly float CurrentHealth;
        public readonly float MaxHealth;

        public BossHealthChangedEvent(float currentHealth, float maxHealth)
        {
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
        }
    }

    /// <summary>Se publica cuando el jefe muere — el HUD oculta su barra de vida.</summary>
    public readonly struct BossDefeatedEvent : IGameEvent
    {
    }
}

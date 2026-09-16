using ArenaSurvivor.Core.Events;

namespace ArenaSurvivor.Progression
{
    /// <summary>Se publica al tocar un cofre — LevelUpManager reacciona igual que a subir de nivel.</summary>
    public readonly struct ChestOpenedEvent : IGameEvent
    {
    }
}

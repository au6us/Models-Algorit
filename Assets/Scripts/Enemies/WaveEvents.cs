using ArenaSurvivor.Core.Events;

namespace ArenaSurvivor.Enemies
{
    /// <summary>Se publica justo cuando dispara una oleada sorpresa — pura señal para el HUD.</summary>
    public readonly struct WaveIncomingEvent : IGameEvent
    {
    }
}

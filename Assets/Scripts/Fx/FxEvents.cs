using UnityEngine;
using ArenaSurvivor.Core.Events;

namespace ArenaSurvivor.Fx
{
    /// <summary>Se publica cada vez que el arma de proyectil dispara (una vez por ráfaga, no por bala).</summary>
    public readonly struct WeaponFiredEvent : IGameEvent
    {
    }

    /// <summary>Se publica cada vez que un arma conecta un golpe — dispara chispas de impacto.</summary>
    public readonly struct WeaponImpactEvent : IGameEvent
    {
        public readonly Vector2 Position;

        public WeaponImpactEvent(Vector2 position)
        {
            Position = position;
        }
    }

    /// <summary>
    /// Se publica solo cuando el Orbe Espectral conecta un golpe — es la única arma con
    /// hit-stop: a diferencia de la bala (dispara todo el tiempo), el orbe conecta cada
    /// tanto, así que frenar el juego un instante se siente especial y no se vuelve repetitivo.
    /// </summary>
    public readonly struct OrbitalHitConnectedEvent : IGameEvent
    {
        public readonly float StopDuration;
        public readonly float TimeScale;

        public OrbitalHitConnectedEvent(float stopDuration, float timeScale)
        {
            StopDuration = stopDuration;
            TimeScale = timeScale;
        }
    }

    /// <summary>
    /// Se publica cada vez que alguien (jugador o enemigo) recibe daño, para mostrar el
    /// número flotante correspondiente. Lo publican los DamageNumberSpawner locales — no
    /// Health, que se mantiene genérico y ajeno al EventManager (ver Health.cs).
    /// </summary>
    public readonly struct FloatingDamageTextEvent : IGameEvent
    {
        public readonly Vector2 Position;
        public readonly float Amount;
        public readonly Color Color;

        public FloatingDamageTextEvent(Vector2 position, float amount, Color color)
        {
            Position = position;
            Amount = amount;
            Color = color;
        }
    }
}

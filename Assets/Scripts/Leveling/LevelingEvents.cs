using ArenaSurvivor.Core.Events;

namespace ArenaSurvivor.Leveling
{
    /// <summary>
    /// Se publica cuando LevelUpManager efectivamente abre el panel de elegir mejora —
    /// sin importar si lo disparó subir de nivel o abrir un cofre. Todo lo que reacciona a
    /// "el panel está abierto" (música, cámara, sonido) escucha esto en vez de escuchar por
    /// separado cada posible disparador.
    /// </summary>
    public readonly struct ChoicePanelOpenedEvent : IGameEvent
    {
    }

    /// <summary>
    /// Se publica cuando el jugador elige una mejora en el panel (el panel se cierra
    /// y el juego reanuda). Sirve para "des-atenuar" la música y para el sonido de click —
    /// nadie que escuche esto necesita saber nada del panel en sí.
    /// </summary>
    public readonly struct UpgradeChosenEvent : IGameEvent
    {
    }
}

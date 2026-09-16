using System.Collections.Generic;

namespace ArenaSurvivor.Progression
{
    /// <summary>
    /// Lista de cofres activos en este momento (mismo patrón que EnemyRegistry). La usa el
    /// cursor del jugador para saber si hay que apuntar la manito hacia alguno cercano, sin
    /// que el cursor necesite acoplarse al ChestSpawner.
    /// </summary>
    public static class ChestRegistry
    {
        private static readonly List<Chest> Active = new List<Chest>();

        public static IReadOnlyList<Chest> ActiveChests => Active;

        public static void Register(Chest chest)
        {
            if (!Active.Contains(chest))
            {
                Active.Add(chest);
            }
        }

        public static void Unregister(Chest chest)
        {
            Active.Remove(chest);
        }
    }
}

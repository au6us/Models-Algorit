using UnityEngine;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Progression
{
    /// <summary>Al morir un enemigo especial (elite/jefe), aparece un cofre en su posición.</summary>
    public class ChestSpawner : MonoBehaviour
    {
        [SerializeField] private Chest chestPrefab;

        private ChestFactory _factory;

        private void Awake()
        {
            _factory = new ChestFactory(chestPrefab, transform);
        }

        private void OnEnable()
        {
            EventManager.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnEnemyKilled(EnemyKilledEvent gameEvent)
        {
            if (gameEvent.DropsChest)
            {
                _factory.Create(gameEvent.Position);
            }
        }
    }
}

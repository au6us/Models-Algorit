using UnityEngine;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Fx
{
    /// <summary>
    /// Un pulso blanco que se expande y se desvanece al morir un enemigo elite o el jefe —
    /// nada más que un "clic" visual para remarcar esas muertes puntuales, no las de todos
    /// los enemigos (por eso filtra por DropsChest, la misma marca que ya usan los cofres).
    /// </summary>
    public class EliteKillRipple : MonoBehaviour
    {
        [SerializeField] private PooledParticleEffect prefab;

        private ParticleEffectFactory _factory;

        private void Awake()
        {
            _factory = new ParticleEffectFactory(prefab, transform);
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
            if (!gameEvent.DropsChest)
            {
                return;
            }

            _factory.Spawn(gameEvent.Position, gameEvent.Color);
        }
    }
}

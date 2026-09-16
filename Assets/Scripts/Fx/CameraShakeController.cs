using Unity.Cinemachine;
using UnityEngine;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Player;

namespace ArenaSurvivor.Fx
{
    /// <summary>
    /// Sacude la cámara (Cinemachine Impulse) al recibir daño y al matar enemigos. No hay
    /// "golpe fuerte" como tal en este juego, así que la fuerza de la sacudida al matar un
    /// enemigo escala con su ExperienceReward: un Swarm casi no se nota, un Tank sí — usando
    /// un dato que YA existe (no hace falta un arma nueva ni un enemigo que "explote").
    /// </summary>
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class CameraShakeController : MonoBehaviour
    {
        [SerializeField] private float playerHitShakeForce = 0.3f;

        [Tooltip("Enemigos con ExperienceReward por debajo de esto no sacuden la cámara al morir.")]
        [SerializeField] private float enemyKillShakeThresholdXp = 4f;

        [Tooltip("Multiplicador: fuerza de la sacudida = ExperienceReward * este valor.")]
        [SerializeField] private float enemyKillShakePerXp = 0.05f;

        private CinemachineImpulseSource _impulseSource;
        private float _lastKnownPlayerHealth = -1f;

        private void Awake()
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        private void OnEnable()
        {
            EventManager.Subscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            EventManager.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            EventManager.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnPlayerHealthChanged(PlayerHealthChangedEvent gameEvent)
        {
            // El primer evento (estado inicial al arrancar la partida) no debe sacudir nada;
            // el sentinel negativo hace que ese primer chequeo se salte solo.
            if (_lastKnownPlayerHealth >= 0f && gameEvent.CurrentHealth < _lastKnownPlayerHealth)
            {
                _impulseSource.GenerateImpulseWithForce(playerHitShakeForce);
            }

            _lastKnownPlayerHealth = gameEvent.CurrentHealth;
        }

        private void OnEnemyKilled(EnemyKilledEvent gameEvent)
        {
            if (gameEvent.ExperienceReward < enemyKillShakeThresholdXp)
            {
                return;
            }

            _impulseSource.GenerateImpulseWithForce(gameEvent.ExperienceReward * enemyKillShakePerXp);
        }
    }
}

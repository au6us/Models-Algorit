using UnityEngine;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Events;

namespace ArenaSurvivor.Fx
{
    /// <summary>
    /// Publica un FloatingDamageTextEvent cada vez que SU PROPIO Health pierde vida. Un
    /// componente igual va en el Player (color rojo) y en el prefab de Enemy (color blanco) —
    /// cada uno decide su color, ninguno sabe nada de pooling ni de cómo se dibuja el número.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DamageNumberSpawner : MonoBehaviour
    {
        [SerializeField] private Color color = Color.white;

        private Health _health;
        private float _lastKnownHealth;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.HealthChanged += HandleHealthChanged;
        }

        private void Start()
        {
            _lastKnownHealth = _health.CurrentHealth;
        }

        private void OnDisable()
        {
            _health.HealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (current < _lastKnownHealth)
            {
                EventManager.Publish(new FloatingDamageTextEvent(transform.position, _lastKnownHealth - current, color));
            }

            _lastKnownHealth = current;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Fx;

namespace ArenaSurvivor.Weapons
{
    /// <summary>
    /// Collider hijo del OrbitalWeapon: aplica daño con un cooldown por enemigo tocado.
    /// Cada golpe también empuja al enemigo hacia afuera y frena un instante el juego
    /// (hit-stop) — es la única arma con ese "peso" extra, porque a diferencia de la bala
    /// (que dispara todo el tiempo) esta conecta cada tanto y no se vuelve repetitivo.
    /// </summary>
    public class OrbitalHitbox : MonoBehaviour
    {
        private const float HitCooldown = 0.5f;

        [Tooltip("Fuerza con la que este golpe empuja al enemigo hacia afuera.")]
        [SerializeField] private float knockbackForce = 5f;

        [Tooltip("Segundos que el juego se frena en cámara lenta al conectar un golpe.")]
        [SerializeField] private float hitStopDuration = 0.05f;

        [Tooltip("A qué velocidad queda el tiempo durante el hit-stop (0 = freeze total).")]
        [SerializeField] private float hitStopTimeScale = 0.05f;

        private OrbitalWeapon _owner;
        private readonly Dictionary<Enemy, float> _lastHitTimes = new Dictionary<Enemy, float>();

        public void SetOwner(OrbitalWeapon owner)
        {
            _owner = owner;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHit(other);

        private void OnTriggerStay2D(Collider2D other) => TryHit(other);

        private void TryHit(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                return;
            }

            if (!other.TryGetComponent<Enemy>(out var enemy))
            {
                return;
            }

            if (!other.TryGetComponent<IDamageable>(out var damageable))
            {
                return;
            }

            if (_lastHitTimes.TryGetValue(enemy, out var lastHitTime) && Time.time - lastHitTime < HitCooldown)
            {
                return;
            }

            damageable.TakeDamage(_owner.CurrentDamage);

            var direction = ((Vector2)enemy.transform.position - (Vector2)_owner.transform.position).normalized;
            enemy.ApplyKnockback(direction, knockbackForce);

            EventManager.Publish(new WeaponImpactEvent(enemy.transform.position));
            EventManager.Publish(new OrbitalHitConnectedEvent(hitStopDuration, hitStopTimeScale));

            _lastHitTimes[enemy] = Time.time;
        }
    }
}

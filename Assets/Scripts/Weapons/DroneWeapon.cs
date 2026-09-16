using UnityEngine;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Fx;

namespace ArenaSurvivor.Weapons
{
    /// <summary>
    /// Dron acompañante: flota cerca del jugador (ver DroneMovement) y dispara solo, de
    /// forma independiente, al enemigo más cercano A ÉL — reusa el mismo Projectile/Factory
    /// que la bala mágica, solo que el origen del disparo es el dron, no el jugador.
    /// </summary>
    public class DroneWeapon : WeaponBase
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform droneVisual;
        [SerializeField] private float range = 10f;
        [SerializeField] private float knockbackForce = 2f;

        private ProjectileFactory _factory;

        protected override void Awake()
        {
            base.Awake();
            _factory = new ProjectileFactory(projectilePrefab, transform);
        }

        protected override void Fire()
        {
            if (droneVisual == null)
            {
                return;
            }

            var target = FindNearestEnemy();
            if (target == null)
            {
                return;
            }

            EventManager.Publish(new WeaponFiredEvent());

            var origin = (Vector2)droneVisual.position;
            var direction = ((Vector2)target.transform.position - origin).normalized;
            _factory.Create(origin, direction, Data.ProjectileSpeed, CurrentDamage, Data.ProjectileLifetime, knockbackForce);
        }

        // El visual es un hijo aparte: sin esto, seguiría flotando a la vista aunque el
        // arma todavía no esté desbloqueada (arranca desactivada hasta elegirla en el panel).
        private void OnEnable()
        {
            if (droneVisual != null)
            {
                droneVisual.gameObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (droneVisual != null)
            {
                droneVisual.gameObject.SetActive(false);
            }
        }

        private Enemy FindNearestEnemy()
        {
            Enemy nearest = null;
            var nearestDistanceSquared = float.MaxValue;
            var rangeSquared = range * range;
            var origin = (Vector2)droneVisual.position;

            var enemies = EnemyRegistry.ActiveEnemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                var distanceSquared = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (distanceSquared <= rangeSquared && distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = enemy;
                }
            }

            return nearest;
        }
    }
}

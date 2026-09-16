using System.Collections;
using UnityEngine;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Fx;

namespace ArenaSurvivor.Weapons
{
    /// <summary>
    /// Haz perforante: dispara en línea recta hacia el enemigo más cercano y atraviesa a
    /// todos los que estén en el camino (no se detiene en el primero, a diferencia de la
    /// bala). Es instantáneo (hit-scan, sin proyectil viajando) — el LineRenderer es solo
    /// el feedback visual del disparo, no participa en la detección de impacto.
    /// </summary>
    public class LaserWeapon : WeaponBase
    {
        [SerializeField] private float range = 12f;

        [Tooltip("Ancho del pasillo alrededor de la línea de disparo que cuenta como 'en el haz'.")]
        [SerializeField] private float beamWidth = 0.6f;

        [SerializeField] private float knockbackForce = 2f;
        [SerializeField] private LineRenderer beamVisual;
        [SerializeField] private float beamVisualDuration = 0.15f;

        private Coroutine _visualRoutine;

        protected override void Fire()
        {
            var aimTarget = FindNearestEnemy();
            if (aimTarget == null)
            {
                return;
            }

            var origin = (Vector2)transform.position;
            var direction = ((Vector2)aimTarget.transform.position - origin).normalized;
            var damage = CurrentDamage;
            var rangeSquared = range * range;

            EventManager.Publish(new WeaponFiredEvent());

            var enemies = EnemyRegistry.ActiveEnemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                var toEnemy = (Vector2)enemy.transform.position - origin;
                var forwardDistance = Vector2.Dot(toEnemy, direction);

                if (forwardDistance < 0f || forwardDistance * forwardDistance > rangeSquared)
                {
                    continue;
                }

                var perpendicular = toEnemy - direction * forwardDistance;
                if (perpendicular.sqrMagnitude > beamWidth * beamWidth)
                {
                    continue;
                }

                if (enemy.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(damage);
                }

                enemy.ApplyKnockback(direction, knockbackForce);
                EventManager.Publish(new WeaponImpactEvent(enemy.transform.position));
            }

            ShowBeam(origin, origin + direction * range);
        }

        private void ShowBeam(Vector2 from, Vector2 to)
        {
            if (beamVisual == null)
            {
                return;
            }

            beamVisual.positionCount = 2;
            beamVisual.SetPosition(0, from);
            beamVisual.SetPosition(1, to);

            if (_visualRoutine != null)
            {
                StopCoroutine(_visualRoutine);
            }

            _visualRoutine = StartCoroutine(BeamVisualRoutine());
        }

        private IEnumerator BeamVisualRoutine()
        {
            beamVisual.enabled = true;
            yield return new WaitForSeconds(beamVisualDuration);
            beamVisual.enabled = false;
            _visualRoutine = null;
        }

        private Enemy FindNearestEnemy()
        {
            Enemy nearest = null;
            var nearestDistanceSquared = float.MaxValue;
            var rangeSquared = range * range;
            var origin = (Vector2)transform.position;

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

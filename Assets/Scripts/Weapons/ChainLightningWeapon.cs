using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Fx;

namespace ArenaSurvivor.Weapons
{
    /// <summary>
    /// Cadena eléctrica: pega en el enemigo más cercano y salta al próximo enemigo sin
    /// tocar dentro de un radio corto, hasta un máximo de saltos. Cada salto hace el mismo
    /// daño (sin caída) — la gracia es alcanzar a varios enemigos, no pegar más fuerte.
    /// </summary>
    public class ChainLightningWeapon : WeaponBase
    {
        [SerializeField] private float range = 9f;
        [SerializeField] private int maxChainJumps = 3;
        [SerializeField] private float chainRange = 4f;
        [SerializeField] private float knockbackForce = 2f;
        [SerializeField] private LineRenderer chainVisual;
        [SerializeField] private float visualDuration = 0.2f;

        private readonly List<Enemy> _hitEnemies = new List<Enemy>();
        private readonly List<Vector3> _visualPoints = new List<Vector3>();
        private Coroutine _visualRoutine;

        protected override void Fire()
        {
            var first = FindNearest(transform.position, range, null);
            if (first == null)
            {
                return;
            }

            EventManager.Publish(new WeaponFiredEvent());

            _hitEnemies.Clear();
            _visualPoints.Clear();
            _visualPoints.Add(transform.position);

            var damage = CurrentDamage;
            var current = first;

            for (var jump = 0; jump < maxChainJumps && current != null; jump++)
            {
                _hitEnemies.Add(current);
                _visualPoints.Add(current.transform.position);

                var hitPosition = current.transform.position;
                var direction = ((Vector2)hitPosition - (Vector2)transform.position).normalized;

                if (current.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(damage);
                }

                current.ApplyKnockback(direction, knockbackForce);
                EventManager.Publish(new WeaponImpactEvent(hitPosition));

                current = FindNearest(hitPosition, chainRange, _hitEnemies);
            }

            ShowVisual();
        }

        private Enemy FindNearest(Vector2 origin, float maxRange, List<Enemy> exclude)
        {
            Enemy nearest = null;
            var nearestDistanceSquared = float.MaxValue;
            var rangeSquared = maxRange * maxRange;

            var enemies = EnemyRegistry.ActiveEnemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (exclude != null && exclude.Contains(enemy))
                {
                    continue;
                }

                var distanceSquared = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (distanceSquared <= rangeSquared && distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        private void ShowVisual()
        {
            if (chainVisual == null)
            {
                return;
            }

            chainVisual.positionCount = _visualPoints.Count;
            chainVisual.SetPositions(_visualPoints.ToArray());

            if (_visualRoutine != null)
            {
                StopCoroutine(_visualRoutine);
            }

            _visualRoutine = StartCoroutine(VisualRoutine());
        }

        private IEnumerator VisualRoutine()
        {
            chainVisual.enabled = true;
            yield return new WaitForSeconds(visualDuration);
            chainVisual.enabled = false;
            _visualRoutine = null;
        }
    }
}

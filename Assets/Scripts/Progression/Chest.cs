using System;
using UnityEngine;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Core.Pooling;

namespace ArenaSurvivor.Progression
{
    /// <summary>
    /// Cofre pooleado que sueltan los enemigos especiales (elite/jefe): a diferencia de la
    /// gema de XP, no tiene imán — hay que caminar hasta él. Al tocarlo, publica
    /// ChestOpenedEvent (Observer) y se libera al pool. No conoce a LevelUpManager.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Chest : MonoBehaviour, IPoolable
    {
        private Action<Chest> _releaseToPool;
        private bool _released;

        public void Initialize(Vector2 position, Action<Chest> releaseToPool)
        {
            transform.position = position;
            _releaseToPool = releaseToPool;
        }

        public void OnSpawn()
        {
            _released = false;
        }

        public void OnDespawn()
        {
        }

        private void OnEnable()
        {
            ChestRegistry.Register(this);
        }

        private void OnDisable()
        {
            ChestRegistry.Unregister(this);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_released || !other.CompareTag("Player"))
            {
                return;
            }

            _released = true;
            EventManager.Publish(new ChestOpenedEvent());
            _releaseToPool?.Invoke(this);
        }
    }
}

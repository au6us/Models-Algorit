using UnityEngine;
using ArenaSurvivor.Core.Pooling;

namespace ArenaSurvivor.Progression
{
    /// <summary>Crea/recicla cofres vía ObjectPool — mismo patrón que EnemyFactory.</summary>
    public class ChestFactory
    {
        private readonly ObjectPool<Chest> _pool;

        public ChestFactory(Chest prefab, Transform poolParent, int prewarmCount = 4)
        {
            _pool = new ObjectPool<Chest>(prefab, poolParent, prewarmCount);
        }

        public void Create(Vector2 position)
        {
            var chest = _pool.Get(position, Quaternion.identity);
            chest.Initialize(position, Release);
        }

        private void Release(Chest chest)
        {
            _pool.Release(chest);
        }
    }
}

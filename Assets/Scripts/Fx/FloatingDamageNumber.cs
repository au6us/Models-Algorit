using System;
using UnityEngine;
using ArenaSurvivor.Core.Pooling;

namespace ArenaSurvivor.Fx
{
    /// <summary>
    /// Un número de daño flotante pooleado: sube, se desvanece y se libera solo — mismo
    /// esquema que Projectile/XpGem (Object Pool).
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class FloatingDamageNumber : MonoBehaviour, IPoolable
    {
        [SerializeField] private float moveSpeed = 1.2f;
        [SerializeField] private float lifetime = 0.6f;

        private TextMesh _textMesh;
        private Action<FloatingDamageNumber> _releaseToPool;
        private Color _color;
        private float _timer;

        private void Awake()
        {
            _textMesh = GetComponent<TextMesh>();
        }

        public void Show(float amount, Color color, Action<FloatingDamageNumber> releaseToPool)
        {
            _releaseToPool = releaseToPool;
            _color = color;
            _textMesh.text = Mathf.CeilToInt(amount).ToString();
            _textMesh.color = color;
        }

        public void OnSpawn()
        {
            _timer = 0f;
        }

        public void OnDespawn()
        {
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            transform.position += Vector3.up * (moveSpeed * Time.deltaTime);

            var t = Mathf.Clamp01(_timer / lifetime);
            var fadedColor = _color;
            fadedColor.a = 1f - t;
            _textMesh.color = fadedColor;

            if (_timer >= lifetime)
            {
                _releaseToPool?.Invoke(this);
            }
        }
    }
}

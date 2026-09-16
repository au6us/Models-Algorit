using System;
using UnityEngine;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Core.Pooling;
using ArenaSurvivor.Fx;

namespace ArenaSurvivor.Enemies
{
    /// <summary>
    /// Comportamiento de un enemigo: perseguir al target y hacer daño de contacto. Las
    /// estadísticas (vida, velocidad, daño, color) las recibe de un EnemyData en Initialize —
    /// el mismo prefab sirve para todos los "tipos" de enemigo, solo cambian los datos.
    /// Se recicla vía ObjectPool (IPoolable) en vez de instanciarse/destruirse cada vez.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour, IPoolable, IKnockbackable
    {
        private const float ContactDamageCooldown = 1f;

        [Tooltip("Fuerza con la que este enemigo empuja al Player al tocarlo.")]
        [SerializeField] private float contactKnockbackForce = 6f;

        [Tooltip("Qué tan rápido se apaga el empujón que recibe este enemigo al ser golpeado.")]
        [SerializeField] private float knockbackDamping = 8f;

        private Rigidbody2D _rigidbody;
        private SpriteRenderer _spriteRenderer;
        private Health _health;
        private SquashStretch _squashStretch;

        private Transform _target;
        private Action<Enemy> _releaseToPool;
        private float _moveSpeed;
        private float _contactDamage;
        private int _experienceReward;
        private bool _isSpecial;
        private bool _isBoss;
        private float _lastContactDamageTime;
        private Vector2 _knockbackVelocity;

        /// <summary>Elite o jefe: dispara el drop de cofre y el indicador fuera de cámara.</summary>
        public bool IsSpecial => _isSpecial;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _health = GetComponent<Health>();
            _squashStretch = GetComponent<SquashStretch>();

            // Se suscribe una sola vez: Awake corre una única vez en la vida del GameObject,
            // aunque el pool lo recicle muchas veces con SetActive.
            _health.Died += HandleDied;
            _health.HealthChanged += HandleHealthChanged;
        }

        /// <summary>Llamado por el EnemyFactory apenas lo saca del pool.</summary>
        public void Initialize(EnemyData data, Transform target, Action<Enemy> releaseToPool)
        {
            _target = target;
            _releaseToPool = releaseToPool;
            _moveSpeed = data.MoveSpeed;
            _contactDamage = data.ContactDamage;
            _experienceReward = data.ExperienceReward;
            _isSpecial = data.IsSpecial;
            _isBoss = data.IsBoss;

            _spriteRenderer.color = data.Color;
            var targetScale = Vector3.one * data.Scale;
            transform.localScale = targetScale;
            // Vuelve a disparar el "pop" con la escala DEFINITIVA del tipo de enemigo: el
            // SquashStretch ya se había disparado solo en OnEnable, pero en ese momento
            // todavía no corrió este Initialize, así que tenía la escala vieja (de la última
            // vez que se usó esta instancia reciclada).
            _squashStretch?.Play(targetScale);

            _health.ConfigureMaxHealth(data.MaxHealth);

            if (_isBoss)
            {
                EventManager.Publish(new BossSpawnedEvent(data.DisplayName, data.MaxHealth));
            }
        }

        public void OnSpawn()
        {
            _lastContactDamageTime = float.NegativeInfinity;
            _knockbackVelocity = Vector2.zero;
        }

        public void OnDespawn()
        {
            _target = null;
            _rigidbody.linearVelocity = Vector2.zero;
        }

        private void OnEnable()
        {
            EnemyRegistry.Register(this);
        }

        private void OnDisable()
        {
            EnemyRegistry.Unregister(this);
        }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            _knockbackVelocity = direction.normalized * force;
        }

        private void FixedUpdate()
        {
            var chaseVelocity = Vector2.zero;
            if (_target != null)
            {
                var direction = ((Vector2)_target.position - _rigidbody.position).normalized;
                chaseVelocity = direction * _moveSpeed;
            }

            _rigidbody.linearVelocity = chaseVelocity + _knockbackVelocity;
            _knockbackVelocity = Vector2.Lerp(_knockbackVelocity, Vector2.zero, knockbackDamping * Time.fixedDeltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other) => TryDealContactDamage(other);

        private void OnTriggerStay2D(Collider2D other) => TryDealContactDamage(other);

        private void TryDealContactDamage(Collider2D other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            if (Time.time - _lastContactDamageTime < ContactDamageCooldown)
            {
                return;
            }

            if (!other.TryGetComponent<IDamageable>(out var damageable))
            {
                return;
            }

            damageable.TakeDamage(_contactDamage);

            if (other.TryGetComponent<IKnockbackable>(out var knockbackable))
            {
                var direction = ((Vector2)other.transform.position - _rigidbody.position).normalized;
                knockbackable.ApplyKnockback(direction, contactKnockbackForce);
            }

            _lastContactDamageTime = Time.time;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_isBoss)
            {
                EventManager.Publish(new BossHealthChangedEvent(current, max));
            }
        }

        private void HandleDied()
        {
            EventManager.Publish(new EnemyKilledEvent(transform.position, _experienceReward, _spriteRenderer.color, _isSpecial));

            if (_isBoss)
            {
                EventManager.Publish(new BossDefeatedEvent());
            }

            _releaseToPool?.Invoke(this);
        }
    }
}

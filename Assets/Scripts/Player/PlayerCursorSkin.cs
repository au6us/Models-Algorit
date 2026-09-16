using UnityEngine;
using ArenaSurvivor.Core.Combat;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Progression;

namespace ArenaSurvivor.Player
{
    /// <summary>
    /// El player ES el cursor de Windows: pinta sobre su propio SpriteRenderer los mismos
    /// estados que antes vivían en un cursor de pantalla — golpe recién recibido,
    /// invulnerable, dasheando, cerca de un cofre. A propósito SIN rotación: la flecha
    /// siempre mira para el mismo lado, como el cursor real (WASD la mueve, no la gira).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerCursorSkin : MonoBehaviour
    {
        [SerializeField] private Sprite arrowSprite;
        [SerializeField] private Sprite busySprite;
        [SerializeField] private Sprite blockedSprite;
        [SerializeField] private Sprite moveSprite;
        [SerializeField] private Sprite linkSprite;

        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerDash playerDash;

        [SerializeField] private float damageFlashDuration = 0.15f;
        [SerializeField] private float chestHintRadius = 3.5f;

        private SpriteRenderer _spriteRenderer;
        private float _lastKnownHealth = -1f;
        private float _damageFlashUntil;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            EventManager.Subscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
        }

        private void OnPlayerHealthChanged(PlayerHealthChangedEvent gameEvent)
        {
            if (_lastKnownHealth >= 0f && gameEvent.CurrentHealth < _lastKnownHealth)
            {
                _damageFlashUntil = Time.unscaledTime + damageFlashDuration;
            }

            _lastKnownHealth = gameEvent.CurrentHealth;
        }

        private void Update()
        {
            // Golpe recién recibido: prioridad máxima, pero solo un instante.
            if (Time.unscaledTime < _damageFlashUntil)
            {
                SetSprite(busySprite);
                return;
            }

            if (playerHealth != null && playerHealth.IsInvulnerable)
            {
                SetSprite(blockedSprite);
                return;
            }

            if (playerDash != null && playerDash.IsDashing)
            {
                SetSprite(moveSprite);
                return;
            }

            if (IsNearChest())
            {
                SetSprite(linkSprite);
                return;
            }

            SetSprite(arrowSprite);
        }

        private bool IsNearChest()
        {
            var origin = (Vector2)transform.position;
            var radiusSquared = chestHintRadius * chestHintRadius;
            var chests = ChestRegistry.ActiveChests;

            for (var i = 0; i < chests.Count; i++)
            {
                var distanceSquared = ((Vector2)chests[i].transform.position - origin).sqrMagnitude;
                if (distanceSquared <= radiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetSprite(Sprite sprite)
        {
            if (_spriteRenderer.sprite != sprite)
            {
                _spriteRenderer.sprite = sprite;
            }
        }
    }
}

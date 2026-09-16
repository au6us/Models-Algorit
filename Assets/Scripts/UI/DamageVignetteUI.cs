using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Player;

namespace ArenaSurvivor.UI
{
    /// <summary>
    /// Flashea un rojo semitransparente al recibir daño, y además pulsa constante (más
    /// suave) mientras la vida está en zona crítica — las dos cosas comparten el mismo
    /// Image, así que se combinan tomando el máximo de las dos en vez de pisarse.
    /// </summary>
    public class DamageVignetteUI : MonoBehaviour
    {
        [SerializeField] private Image vignetteImage;
        [SerializeField] private float flashAlpha = 0.35f;
        [SerializeField] private float fadeDuration = 0.4f;

        [Header("Vida crítica")]
        [SerializeField] [Range(0f, 1f)] private float criticalHealthPercent = 0.25f;
        [SerializeField] private float criticalPulseMinAlpha = 0.05f;
        [SerializeField] private float criticalPulseMaxAlpha = 0.18f;
        [SerializeField] private float criticalPulseSpeed = 3f;

        private float _lastKnownHealth = -1f;
        private float _currentFlashAlpha;
        private bool _isCritical;
        private Coroutine _flashRoutine;

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
                TriggerFlash();
            }

            _lastKnownHealth = gameEvent.CurrentHealth;

            var healthPercent = gameEvent.MaxHealth > 0f ? gameEvent.CurrentHealth / gameEvent.MaxHealth : 0f;
            _isCritical = gameEvent.CurrentHealth > 0f && healthPercent <= criticalHealthPercent;
        }

        private void TriggerFlash()
        {
            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
            }

            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            var elapsed = 0f;
            _currentFlashAlpha = flashAlpha;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _currentFlashAlpha = Mathf.Lerp(flashAlpha, 0f, elapsed / fadeDuration);
                yield return null;
            }

            _currentFlashAlpha = 0f;
            _flashRoutine = null;
        }

        private void Update()
        {
            var pulseAlpha = 0f;

            if (_isCritical)
            {
                var t = (Mathf.Sin(Time.unscaledTime * criticalPulseSpeed) + 1f) * 0.5f;
                pulseAlpha = Mathf.Lerp(criticalPulseMinAlpha, criticalPulseMaxAlpha, t);
            }

            SetAlpha(Mathf.Max(_currentFlashAlpha, pulseAlpha));
        }

        private void SetAlpha(float alpha)
        {
            var color = vignetteImage.color;
            color.a = alpha;
            vignetteImage.color = color;
        }
    }
}

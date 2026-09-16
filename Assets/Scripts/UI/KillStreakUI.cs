using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Fx;

namespace ArenaSurvivor.UI
{
    /// <summary>
    /// "¡Racha x5!" mientras el jugador encadena kills sin pausa entre uno y otro, con una
    /// barrita que se va vaciando para mostrar cuánto falta para que se corte. Puramente
    /// cosmético — no toca ningún stat, solo lee EnemyKilledEvent.
    ///
    /// Usa Time.time (no Time.unscaledTime) a propósito: mientras el panel de nivel está
    /// abierto, timeScale queda en 0 y este reloj se congela con todo lo demás — así, si la
    /// racha se corta, es porque el jugador dejó pasar tiempo de juego real sin matar nada,
    /// nunca porque tardó en elegir una mejora.
    /// </summary>
    public class KillStreakUI : MonoBehaviour
    {
        [SerializeField] private Text streakText;
        [SerializeField] private Image streakBarFill;
        [SerializeField] private float streakWindowSeconds = 2f;
        [SerializeField] private int minStreakToShow = 3;

        private SquashStretch _squashStretch;
        private Vector3 _baseScale;
        private int _streakCount;
        private float _lastKillTime = float.NegativeInfinity;

        private void Awake()
        {
            _squashStretch = streakText.GetComponent<SquashStretch>();
            _baseScale = streakText.transform.localScale;
            streakText.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventManager.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnEnemyKilled(EnemyKilledEvent gameEvent)
        {
            _streakCount = Time.time - _lastKillTime <= streakWindowSeconds ? _streakCount + 1 : 1;
            _lastKillTime = Time.time;

            if (_streakCount < minStreakToShow)
            {
                return;
            }

            streakText.text = $"¡Racha x{_streakCount}!";
            streakText.gameObject.SetActive(true);
            _squashStretch?.Play(_baseScale);
        }

        private void Update()
        {
            if (!streakText.gameObject.activeSelf)
            {
                return;
            }

            var elapsedSinceKill = Time.time - _lastKillTime;

            if (elapsedSinceKill > streakWindowSeconds)
            {
                streakText.gameObject.SetActive(false);
                _streakCount = 0;
                return;
            }

            if (streakBarFill != null)
            {
                streakBarFill.fillAmount = 1f - elapsedSinceKill / streakWindowSeconds;
            }
        }
    }
}

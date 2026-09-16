using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Fx;
using ArenaSurvivor.Player;

namespace ArenaSurvivor.UI
{
    /// <summary>
    /// Aviso grande y transitorio en el centro de pantalla — lo dispara cualquier evento
    /// "anuncio único" del juego (oleada sorpresa, desbloqueo de dash). Cada evento nuevo que
    /// deba avisar algo así solo necesita sumar su propio Subscribe acá, con su propio texto.
    /// </summary>
    public class WaveWarningUI : MonoBehaviour
    {
        [SerializeField] private Text warningText;
        [SerializeField] private float visibleDuration = 2f;

        private SquashStretch _squashStretch;
        private Vector3 _baseScale;
        private Coroutine _routine;

        private void Awake()
        {
            _squashStretch = warningText.GetComponent<SquashStretch>();
            _baseScale = warningText.transform.localScale;
            warningText.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventManager.Subscribe<WaveIncomingEvent>(OnWaveIncoming);
            EventManager.Subscribe<DashUnlockedEvent>(OnDashUnlocked);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<WaveIncomingEvent>(OnWaveIncoming);
            EventManager.Unsubscribe<DashUnlockedEvent>(OnDashUnlocked);
        }

        private void OnWaveIncoming(WaveIncomingEvent gameEvent) => Show("¡OLEADA!");

        private void OnDashUnlocked(DashUnlockedEvent gameEvent) => Show("¡Dash desbloqueado! (Espacio)");

        private void Show(string message)
        {
            warningText.text = message;

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            warningText.gameObject.SetActive(true);
            _squashStretch?.Play(_baseScale);

            yield return new WaitForSeconds(visibleDuration);

            warningText.gameObject.SetActive(false);
            _routine = null;
        }
    }
}

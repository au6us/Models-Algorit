using System.Collections;
using UnityEngine;
using ArenaSurvivor.Core.Events;

namespace ArenaSurvivor.Fx
{
    /// <summary>
    /// Frena el juego un instante (hit-stop) al recibir un OrbitalHitConnectedEvent. Vive
    /// una sola vez en la escena (igual que GameManager) — no es un Singleton de código,
    /// solo un MonoBehaviour más que escucha el EventManager como cualquier otro.
    /// </summary>
    public class HitStopController : MonoBehaviour
    {
        private Coroutine _routine;

        private void OnEnable()
        {
            EventManager.Subscribe<OrbitalHitConnectedEvent>(OnOrbitalHit);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<OrbitalHitConnectedEvent>(OnOrbitalHit);

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
                Time.timeScale = 1f;
            }
        }

        private void OnOrbitalHit(OrbitalHitConnectedEvent gameEvent)
        {
            // Si el juego está pausado (panel de nivel, game over), no hay combate pasando
            // de verdad — no lo pisamos con un timeScale más raro todavía.
            if (Time.timeScale == 0f)
            {
                return;
            }

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(FreezeRoutine(gameEvent.StopDuration, gameEvent.TimeScale));
        }

        private IEnumerator FreezeRoutine(float duration, float timeScale)
        {
            Time.timeScale = timeScale;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
            _routine = null;
        }
    }
}

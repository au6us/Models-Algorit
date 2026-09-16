using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Fx;

namespace ArenaSurvivor.UI
{
    /// <summary>
    /// El chiste de "el jefe termina en la Papelera de reciclaje" al morir — un ícono grande
    /// en el centro de pantalla con su pop de entrada, nada más. Separado de BossHealthBarUI
    /// a propósito: es un festejo puntual, no parte de la barra de vida en sí.
    /// </summary>
    public class BossRecycleBinUI : MonoBehaviour
    {
        [SerializeField] private Image binImage;
        [SerializeField] private float visibleDuration = 1.5f;

        private SquashStretch _squashStretch;
        private Vector3 _baseScale;
        private Coroutine _routine;

        private void Awake()
        {
            _squashStretch = binImage.GetComponent<SquashStretch>();
            _baseScale = binImage.transform.localScale;
            binImage.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventManager.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnBossDefeated(BossDefeatedEvent gameEvent)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            binImage.gameObject.SetActive(true);
            _squashStretch?.Play(_baseScale);

            yield return new WaitForSeconds(visibleDuration);

            binImage.gameObject.SetActive(false);
            _routine = null;
        }
    }
}

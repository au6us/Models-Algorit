using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.UI
{
    /// <summary>Barra de vida del jefe actual — oculta salvo que haya uno vivo en pantalla.</summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text nameText;

        [Tooltip("El 'Copiando salud...' que tapa la barra un instante al aparecer el jefe.")]
        [SerializeField] private GameObject loadingOverlay;
        [SerializeField] private float loadingDuration = 1.2f;

        private Coroutine _loadingRoutine;

        private void Awake()
        {
            panelRoot.SetActive(false);

            if (loadingOverlay != null)
            {
                loadingOverlay.SetActive(false);
            }
        }

        private void OnEnable()
        {
            EventManager.Subscribe<BossSpawnedEvent>(OnBossSpawned);
            EventManager.Subscribe<BossHealthChangedEvent>(OnBossHealthChanged);
            EventManager.Subscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<BossSpawnedEvent>(OnBossSpawned);
            EventManager.Unsubscribe<BossHealthChangedEvent>(OnBossHealthChanged);
            EventManager.Unsubscribe<BossDefeatedEvent>(OnBossDefeated);
        }

        private void OnBossSpawned(BossSpawnedEvent gameEvent)
        {
            panelRoot.SetActive(true);
            fillImage.fillAmount = 1f;

            if (nameText != null)
            {
                nameText.text = gameEvent.DisplayName;
            }

            if (loadingOverlay == null)
            {
                return;
            }

            if (_loadingRoutine != null)
            {
                StopCoroutine(_loadingRoutine);
            }

            _loadingRoutine = StartCoroutine(ShowLoadingOverlay());
        }

        private IEnumerator ShowLoadingOverlay()
        {
            loadingOverlay.SetActive(true);

            yield return new WaitForSeconds(loadingDuration);

            loadingOverlay.SetActive(false);
            _loadingRoutine = null;
        }

        private void OnBossHealthChanged(BossHealthChangedEvent gameEvent)
        {
            // Defensivo: si por algún motivo este evento llega antes que BossSpawnedEvent,
            // igual queremos que la barra ya esté visible con el valor correcto.
            panelRoot.SetActive(true);
            fillImage.fillAmount = gameEvent.MaxHealth <= 0f
                ? 0f
                : Mathf.Clamp01(gameEvent.CurrentHealth / gameEvent.MaxHealth);
        }

        private void OnBossDefeated(BossDefeatedEvent gameEvent)
        {
            panelRoot.SetActive(false);
        }
    }
}

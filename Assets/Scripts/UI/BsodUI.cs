using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Managers;

namespace ArenaSurvivor.UI
{
    /// <summary>
    /// El "pantallazo azul" cuando el jugador muere — reemplaza al panel de game over
    /// genérico solo para la derrota (ganar sigue mostrando el panel normal de GameEndUI).
    /// Vive separado de GameEndUI a propósito: son dos tratamientos visuales completamente
    /// distintos, y cada uno solo necesita escuchar su propio evento.
    /// </summary>
    public class BsodUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI messageText;

        private bool _isShowingPanel;

        private void Awake()
        {
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            EventManager.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        private void Update()
        {
            if (_isShowingPanel && Input.GetKeyDown(KeyCode.R))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        private void OnGameOver(GameOverEvent gameEvent)
        {
            messageText.text = ":(\n\n" +
                "Tu PC tuvo un problema y tiene que reiniciarse.\n\n" +
                "Qué pena que hayas muerto... otra vez. ¿Reintentar?\n\n" +
                "Presioná R para reintentar";

            _isShowingPanel = true;
            panelRoot.SetActive(true);
        }
    }
}

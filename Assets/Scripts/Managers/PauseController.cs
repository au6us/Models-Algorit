using UnityEngine;

namespace ArenaSurvivor.Managers
{
    /// <summary>
    /// Pausa/despausa el gameplay con ESC — pisando Time.timeScale, el mismo mecanismo que
    /// ya usan el panel de nivel, el cofre y game over (ver WindowTitleBarUI/HitStopController),
    /// así que todo lo que ya reacciona a "Time.timeScale == 0" (la barra de título se pone
    /// gris, HitStopController no dispara otro freeze encima) se integra solo, sin tocar
    /// nada de eso. Todavía sin panel/Canvas propio — eso viene después.
    ///
    /// ESC nunca pisa una pausa que abrió otro sistema (panel de nivel, cofre, game over):
    /// si Time.timeScale ya estaba en 0 por otra razón cuando ESC togglearía a "pausado",
    /// no hace nada — evita despausar por accidente algo que este script no abrió.
    /// </summary>
    public class PauseController : MonoBehaviour
    {
        private bool _isPaused;

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            if (!_isPaused && Time.timeScale == 0f)
            {
                return;
            }

            _isPaused = !_isPaused;
            Time.timeScale = _isPaused ? 0f : 1f;
        }

        private void OnDisable()
        {
            if (_isPaused)
            {
                _isPaused = false;
                Time.timeScale = 1f;
            }
        }
    }
}

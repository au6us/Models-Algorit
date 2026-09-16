using UnityEngine;
using UnityEngine.UI;

namespace ArenaSurvivor.UI
{
    /// <summary>
    /// La barra de título de arriba se pone "inactiva" (el gris pálido de una ventana de
    /// Windows sin foco) cada vez que el juego está en pausa — panel de nivel, cofre o game
    /// over, no importa cuál: todos pisan Time.timeScale a 0, así que con leer eso alcanza,
    /// sin acoplarse a LevelUpManager/GameManager/GameEndUI por separado.
    /// </summary>
    public class WindowTitleBarUI : MonoBehaviour
    {
        [SerializeField] private Image barImage;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite inactiveSprite;

        private bool _isActive = true;

        private void Update()
        {
            var shouldBeActive = Time.timeScale > 0f;

            if (shouldBeActive == _isActive)
            {
                return;
            }

            _isActive = shouldBeActive;
            barImage.sprite = _isActive ? activeSprite : inactiveSprite;
        }
    }
}

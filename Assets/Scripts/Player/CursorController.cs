using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Leveling;

namespace ArenaSurvivor.Player
{
    /// <summary>
    /// El cursor de mouse que aparece SOLO mientras hay un menú abierto (el panel de subir
    /// de nivel/cofre). El resto del tiempo el propio Player hace de cursor en el mundo
    /// (ver PlayerCursorSkin) y el mouse real de Windows queda oculto, sin nada dibujado en
    /// pantalla. Acá el único estado que importa es "¿estoy sobre un botón clickeable?".
    /// </summary>
    public class CursorController : MonoBehaviour
    {
        [SerializeField] private Image cursorImage;
        [SerializeField] private Sprite arrowSprite;
        [SerializeField] private Sprite linkSprite;

        private RectTransform _canvasRect;
        private RectTransform _cursorRect;
        private bool _isHoveringUi;
        private bool _isMenuOpen;

        private void Awake()
        {
            _canvasRect = (RectTransform)transform;
            _cursorRect = cursorImage.rectTransform;
        }

        private void OnEnable()
        {
            Cursor.visible = false;
            EventManager.Subscribe<ChoicePanelOpenedEvent>(OnPanelOpened);
            EventManager.Subscribe<UpgradeChosenEvent>(OnPanelClosed);
            SetMenuOpen(false);
        }

        private void OnDisable()
        {
            Cursor.visible = true;
            EventManager.Unsubscribe<ChoicePanelOpenedEvent>(OnPanelOpened);
            EventManager.Unsubscribe<UpgradeChosenEvent>(OnPanelClosed);
        }

        /// <summary>Lo llaman los CursorHoverHint de los botones al pasar el mouse por encima.</summary>
        public void SetHoveringUi(bool hovering) => _isHoveringUi = hovering;

        private void OnPanelOpened(ChoicePanelOpenedEvent gameEvent) => SetMenuOpen(true);

        private void OnPanelClosed(UpgradeChosenEvent gameEvent) => SetMenuOpen(false);

        private void SetMenuOpen(bool open)
        {
            _isMenuOpen = open;
            _isHoveringUi = false;
            cursorImage.gameObject.SetActive(open);
        }

        private void Update()
        {
            if (!_isMenuOpen)
            {
                return;
            }

            FollowMouse();
            cursorImage.sprite = _isHoveringUi ? linkSprite : arrowSprite;
        }

        private void FollowMouse()
        {
            if (Mouse.current == null)
            {
                return;
            }

            var screenPos = Mouse.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, null, out var localPoint);
            _cursorRect.anchoredPosition = localPoint;
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

namespace ArenaSurvivor.Player
{
    /// <summary>
    /// Avisa al CursorController cuando el mouse pasa por encima de este UI — así el cursor
    /// cambia a la manito mientras hay algo clickeable debajo, como en Windows real.
    /// No sabe nada de qué botón es ni qué hace: es un simple mensajero.
    /// </summary>
    public class CursorHoverHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CursorController _cursorController;

        private void Awake()
        {
            _cursorController = GetComponentInParent<CursorController>();
        }

        public void OnPointerEnter(PointerEventData eventData) => _cursorController?.SetHoveringUi(true);

        public void OnPointerExit(PointerEventData eventData) => _cursorController?.SetHoveringUi(false);

        private void OnDisable()
        {
            // Si el botón se desactiva mientras el mouse seguía "adentro" (ej: se cierra el
            // panel de golpe al elegir), OnPointerExit nunca llega — sin esto el cursor
            // quedaría pegado en modo "manito" para siempre.
            _cursorController?.SetHoveringUi(false);
        }
    }
}

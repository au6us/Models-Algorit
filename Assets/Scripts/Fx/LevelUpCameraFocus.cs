using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using ArenaSurvivor.Core.Events;
using ArenaSurvivor.Leveling;

namespace ArenaSurvivor.Fx
{
    /// <summary>
    /// Al subir de nivel, la cámara hace un acercamiento y se queda ahí mientras el jugador
    /// decide la mejora (no un golpecito que vuelve solo) — y mientras dura ese acercamiento,
    /// sigue un poco al mouse (sin llegar nunca al extremo) para que la pausa de decisión se
    /// sienta viva. Vuelve a la normalidad recién cuando se elige una opción.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class LevelUpCameraFocus : MonoBehaviour
    {
        [SerializeField] private float zoomInPercent = 0.08f;
        [SerializeField] private float zoomTransitionDuration = 0.35f;

        [Tooltip("Cuánto se puede llegar a mover la cámara hacia el mouse, en unidades del mundo.")]
        [SerializeField] private float maxMouseFollowOffset = 1.5f;

        [Tooltip("Qué tan rápido alcanza el offset objetivo (más alto = responde más rápido).")]
        [SerializeField] private float mouseFollowSmoothing = 5f;

        private CinemachineCamera _camera;
        private CinemachinePositionComposer _positionComposer;
        private float _baseOrthoSize;
        private Vector3 _originalDamping;
        private bool _isFocused;
        private Coroutine _zoomRoutine;

        private void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();
            _positionComposer = GetComponent<CinemachinePositionComposer>();
            _baseOrthoSize = _camera.Lens.OrthographicSize;

            if (_positionComposer != null)
            {
                _originalDamping = _positionComposer.Damping;
            }
        }

        private void OnEnable()
        {
            EventManager.Subscribe<ChoicePanelOpenedEvent>(OnPanelOpened);
            EventManager.Subscribe<UpgradeChosenEvent>(OnUpgradeChosen);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<ChoicePanelOpenedEvent>(OnPanelOpened);
            EventManager.Unsubscribe<UpgradeChosenEvent>(OnUpgradeChosen);
        }

        private void OnPanelOpened(ChoicePanelOpenedEvent gameEvent)
        {
            _isFocused = true;
            StartZoomTransition(_baseOrthoSize * (1f - zoomInPercent));

            // El panel pone timeScale en 0, y el Composer amortigua su movimiento usando un
            // deltaTime que también queda en 0 — sin esto, el seguimiento del mouse calcula
            // bien el offset (se ve en el campo) pero la cámara nunca llega a moverse porque
            // el suavizado de Cinemachine se queda esperando un tiempo que no avanza. El
            // suavizado propio (Lerp con tiempo real, más abajo) ya le da todo el "sigue
            // suave" que hace falta, así que sacarle el de Cinemachine acá no se nota feo.
            if (_positionComposer != null)
            {
                _positionComposer.Damping = Vector3.zero;
            }
        }

        private void OnUpgradeChosen(UpgradeChosenEvent gameEvent)
        {
            _isFocused = false;
            StartZoomTransition(_baseOrthoSize);

            if (_positionComposer != null)
            {
                _positionComposer.Damping = _originalDamping;
            }
        }

        private void Update()
        {
            if (_positionComposer == null)
            {
                return;
            }

            var targetOffset = Vector3.zero;

            if (_isFocused && Mouse.current != null)
            {
                var mousePosition = Mouse.current.position.ReadValue();
                var normalizedX = Mathf.Clamp((mousePosition.x / Screen.width - 0.5f) * 2f, -1f, 1f);
                var normalizedY = Mathf.Clamp((mousePosition.y / Screen.height - 0.5f) * 2f, -1f, 1f);
                targetOffset = new Vector3(normalizedX, normalizedY, 0f) * maxMouseFollowOffset;
            }

            // Tiempo real: esto tiene que seguir animándose con el juego pausado (timeScale 0)
            // mientras el panel de nivel está abierto, y también volver suave a 0 al cerrarse.
            _positionComposer.TargetOffset = Vector3.Lerp(_positionComposer.TargetOffset, targetOffset,
                mouseFollowSmoothing * Time.unscaledDeltaTime);
        }

        private void StartZoomTransition(float targetSize)
        {
            if (_zoomRoutine != null)
            {
                StopCoroutine(_zoomRoutine);
            }

            _zoomRoutine = StartCoroutine(ZoomRoutine(targetSize));
        }

        private IEnumerator ZoomRoutine(float targetSize)
        {
            var startSize = _camera.Lens.OrthographicSize;
            var elapsed = 0f;

            while (elapsed < zoomTransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetOrthoSize(Mathf.Lerp(startSize, targetSize, elapsed / zoomTransitionDuration));
                yield return null;
            }

            SetOrthoSize(targetSize);
            _zoomRoutine = null;
        }

        private void SetOrthoSize(float size)
        {
            var lens = _camera.Lens;
            lens.OrthographicSize = size;
            _camera.Lens = lens;
        }
    }
}

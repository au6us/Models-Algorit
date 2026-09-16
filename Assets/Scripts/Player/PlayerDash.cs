using UnityEngine;
using UnityEngine.InputSystem;
using ArenaSurvivor.Core.Events;

namespace ArenaSurvivor.Player
{
    /// <summary>
    /// El "dash" del Windows: un empujón corto y rápido en la última dirección en la que se
    /// movió, con cooldown. Arranca bloqueado y se desbloquea solo al llegar a cierto nivel
    /// (Observer: escucha PlayerLeveledUpEvent, no conoce a PlayerLeveling directamente).
    ///
    /// Se ejecuta DESPUÉS de PlayerController (DefaultExecutionOrder) para poder pisar la
    /// velocidad del Rigidbody2D solo mientras dura el dash, sin que PlayerController tenga
    /// que saber que esto existe — cada uno solo sabe escribir su propia velocidad.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerDash : MonoBehaviour
    {
        [SerializeField] private int unlockAtLevel = 2;
        [SerializeField] private float dashSpeed = 18f;
        [SerializeField] private float dashDuration = 0.18f;
        [SerializeField] private float dashCooldown = 2.5f;

        private Rigidbody2D _rigidbody;
        private InputSystem_Actions _inputActions;
        private Vector2 _lastMoveDirection = Vector2.right;
        private float _dashEndTime;
        private float _dashReadyTime;

        public bool IsUnlocked { get; private set; }
        public bool IsDashing => Time.time < _dashEndTime;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _inputActions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _inputActions ??= new InputSystem_Actions();

            _inputActions.Player.Enable();
            _inputActions.Player.Move.performed += OnMovePerformed;
            // El dash cuelga del botón de Jump (Espacio): no se usa para nada más en este
            // juego 2D top-down, así que lo reaprovechamos en vez de tocar el asset de Input.
            _inputActions.Player.Jump.performed += OnDashPerformed;

            EventManager.Subscribe<PlayerLeveledUpEvent>(OnPlayerLeveledUp);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe<PlayerLeveledUpEvent>(OnPlayerLeveledUp);

            if (_inputActions == null)
            {
                return;
            }

            _inputActions.Player.Move.performed -= OnMovePerformed;
            _inputActions.Player.Jump.performed -= OnDashPerformed;
            _inputActions.Player.Disable();
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            var value = context.ReadValue<Vector2>();
            if (value.sqrMagnitude > 0.01f)
            {
                _lastMoveDirection = value.normalized;
            }
        }

        private void OnDashPerformed(InputAction.CallbackContext context)
        {
            if (!IsUnlocked || Time.time < _dashReadyTime || IsDashing)
            {
                return;
            }

            _dashEndTime = Time.time + dashDuration;
            _dashReadyTime = Time.time + dashCooldown;
        }

        private void OnPlayerLeveledUp(PlayerLeveledUpEvent gameEvent)
        {
            if (IsUnlocked || gameEvent.NewLevel < unlockAtLevel)
            {
                return;
            }

            IsUnlocked = true;
            EventManager.Publish(new DashUnlockedEvent());
        }

        private void FixedUpdate()
        {
            if (!IsDashing)
            {
                return;
            }

            _rigidbody.linearVelocity = _lastMoveDirection * dashSpeed;
        }
    }
}

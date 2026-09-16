using UnityEngine;
using UnityEngine.InputSystem;
using ArenaSurvivor.Core.Combat;

namespace ArenaSurvivor.Player
{
    /// <summary>
    /// Responsable únicamente del movimiento del jugador: lee el input y mueve el Rigidbody2D.
    /// No sabe nada de vida, armas ni nivel — esos viven en sus propios componentes (SRP).
    /// También puede recibir un empujón (IKnockbackable) sin que quien lo golpea sepa nada de
    /// cómo se mueve el jugador — solo pide "empujalo para este lado con esta fuerza".
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour, IKnockbackable
    {
        [SerializeField] private float moveSpeed = 5f;

        [Tooltip("Qué tan rápido se apaga el empujón de un golpe (más alto = se frena antes).")]
        [SerializeField] private float knockbackDamping = 10f;

        private Rigidbody2D _rigidbody;
        private PlayerStats _stats;
        private InputSystem_Actions _inputActions;
        private Vector2 _moveInput;
        private Vector2 _knockbackVelocity;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _stats = GetComponent<PlayerStats>();
            _inputActions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            // Defensivo: si Unity recompila mientras el juego está en Play, hace un domain
            // reload que resetea campos no serializados como este sin volver a llamar a
            // Awake() — sin este chequeo, _inputActions quedaba null y tiraba
            // NullReferenceException acá.
            _inputActions ??= new InputSystem_Actions();

            _inputActions.Player.Enable();
            _inputActions.Player.Move.performed += OnMovePerformed;
            _inputActions.Player.Move.canceled += OnMoveCanceled;
        }

        private void OnDisable()
        {
            if (_inputActions == null)
            {
                return;
            }

            _inputActions.Player.Move.performed -= OnMovePerformed;
            _inputActions.Player.Move.canceled -= OnMoveCanceled;
            _inputActions.Player.Disable();
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            _moveInput = Vector2.zero;
        }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            _knockbackVelocity = direction.normalized * force;
        }

        private void FixedUpdate()
        {
            var speedMultiplier = _stats != null ? _stats.MoveSpeedMultiplier : 1f;
            _rigidbody.linearVelocity = _moveInput * moveSpeed * speedMultiplier + _knockbackVelocity;
            _knockbackVelocity = Vector2.Lerp(_knockbackVelocity, Vector2.zero, knockbackDamping * Time.fixedDeltaTime);
        }
    }
}

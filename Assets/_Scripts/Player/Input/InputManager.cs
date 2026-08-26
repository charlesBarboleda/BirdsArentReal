using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Central input reader. Owns the generated InputActions asset and enables
/// the Player action map. Continuous inputs (Move/Look) are exposed as
/// polled properties; discrete inputs (Attack/Jump/etc.) are exposed as
/// C# events. Gameplay scripts depend only on this class, never on
/// InputActions directly, so the input backend can change without
/// touching consumers.
/// </summary>
namespace InputSystem
{
    [DefaultExecutionOrder(-100)]
    public class InputManager : MonoBehaviour, InputActions.IPlayerActions
    {
        public static InputManager Instance { get; private set; }

        InputActions _inputActions;

        // --- Continuous inputs: poll these every frame ---
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }

        // --- Discrete inputs: subscribe to these ---
        public event Action AttackPerformed;
        public event Action JumpPerformed;
        public event Action InteractPerformed;
        public event Action InteractCanceled;
        public event Action CrouchPerformed;
        public event Action CrouchCanceled;
        public event Action SprintPerformed;
        public event Action SprintCanceled;
        public event Action PreviousPerformed;
        public event Action NextPerformed;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[InputManager] Duplicate instance on '{gameObject.name}' destroyed.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _inputActions = new InputActions();
            _inputActions.Player.SetCallbacks(this);
        }

        void OnEnable()
        {
            _inputActions?.Player.Enable();
        }

        void OnDisable()
        {
            _inputActions?.Player.Disable();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            _inputActions?.Player.SetCallbacks(null);
            _inputActions?.Dispose();
        }

        // ------------------------------------------------------------------
        // InputActions.IPlayerActions implementation
        // (wired up automatically via SetCallbacks — do not call these manually)
        // ------------------------------------------------------------------

        public void OnMove(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed) AttackPerformed?.Invoke();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed) JumpPerformed?.Invoke();
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            // Interact uses a Hold interaction, so "performed" only fires
            // once the hold duration is met — this is intentional, not a bug.
            if (context.performed) InteractPerformed?.Invoke();
            else if (context.canceled) InteractCanceled?.Invoke();
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if (context.performed) CrouchPerformed?.Invoke();
            else if (context.canceled) CrouchCanceled?.Invoke();
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.performed) SprintPerformed?.Invoke();
            else if (context.canceled) SprintCanceled?.Invoke();
        }

        public void OnPrevious(InputAction.CallbackContext context)
        {
            if (context.performed) PreviousPerformed?.Invoke();
        }

        public void OnNext(InputAction.CallbackContext context)
        {
            if (context.performed) NextPerformed?.Invoke();
        }
    }
}
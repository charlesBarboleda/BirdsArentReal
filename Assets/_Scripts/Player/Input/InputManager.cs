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
        public float AscendInput { get; private set; }
        public float DescendInput { get; private set; }
        public bool AscendPressed => AscendInput > 0.1f;
        public bool DescendPressed => DescendInput > 0.1f;

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
        public event Action DropPerformed;
        public event Action PickupPerformed;

        bool _actionJumpHeld;
        bool _actionCrouchHeld;

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

        void Update()
        {
            float ascend = _actionJumpHeld ? 1f : 0f;
            float descend = _actionCrouchHeld ? 1f : 0f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.isPressed)
                    ascend = 1f;
                if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed)
                    descend = 1f;

                // Fallback direct polling for WASD if Move action is 0 but keys are pressed
                if (MoveInput.sqrMagnitude < 0.01f)
                {
                    float x = 0f;
                    float y = 0f;
                    if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
                    if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
                    if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
                    if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
                    if (x != 0f || y != 0f)
                    {
                        MoveInput = new Vector2(x, y).normalized;
                    }
                }
            }

            AscendInput = ascend;
            DescendInput = descend;
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

        public void OnDrop(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                Debug.Log($"OnDrop fired");
                DropPerformed?.Invoke();
            }
        }

        public void OnPickup(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                Debug.Log($"OnPickup fired");
                PickupPerformed?.Invoke();
            }
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                _actionJumpHeld = true;
                JumpPerformed?.Invoke();
            }
            else if (context.canceled)
            {
                _actionJumpHeld = false;
            }
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
            if (context.performed)
            {
                _actionCrouchHeld = true;
                CrouchPerformed?.Invoke();
            }
            else if (context.canceled)
            {
                _actionCrouchHeld = false;
                CrouchCanceled?.Invoke();
            }
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
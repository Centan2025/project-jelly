using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Jelly
{
    [Serializable]
    public struct JellyInputFrame
    {
        public Vector2 Move;
        public bool Brake;
        public bool SplitPressed;
        public bool MergePressed;
    }

    public class JellyInputAdapter : MonoBehaviour
    {
        public enum ControlMode { DragAnalog, SmoothedTarget, VirtualJoystick }
        public ControlMode mode = ControlMode.DragAnalog;
        public JellyMovementConfig config;
        public bool overrideWithSimulatedInput;
        public JellyInputFrame simulatedInput;

        private Vector2 _touchStart;
        private bool _dragging;
        private float _holdTimer;
        private Vector2 _smoothedMove;
        private bool _uiSplitQueued;
        private bool _uiMergeQueued;
        private bool _uiBrakeHeld;

        public JellyInputFrame Current { get; private set; }

        public void TriggerSplitFromUI() => _uiSplitQueued = true;
        public void TriggerMergeFromUI() => _uiMergeQueued = true;
        public void SetBrakeFromUI(bool held) => _uiBrakeHeld = held;

        public void SetSimulatedInput(Vector2 move, bool brake = false, bool split = false, bool merge = false)
        {
            overrideWithSimulatedInput = true;
            simulatedInput = new JellyInputFrame
            {
                Move = Vector2.ClampMagnitude(move, 1f),
                Brake = brake,
                SplitPressed = split,
                MergePressed = merge
            };
            Current = simulatedInput;
        }

        public void ClearSimulatedInput()
        {
            overrideWithSimulatedInput = false;
            simulatedInput = default;
            Current = default;
        }

        public void SampleInput(float dt)
        {
            if (overrideWithSimulatedInput)
            {
                Current = simulatedInput;
                simulatedInput.SplitPressed = false;
                simulatedInput.MergePressed = false;
                return;
            }

            Vector2 rawMove = Vector2.zero;
            bool brake = _uiBrakeHeld;
            bool split = _uiSplitQueued;
            bool merge = _uiMergeQueued;
            _uiSplitQueued = false;
            _uiMergeQueued = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) rawMove.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) rawMove.x += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) rawMove.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) rawMove.y -= 1f;
                if (kb.LeftShiftKeyPressed() || kb.ctrlKey.isPressed) brake = true;
                if (kb.spaceKey.wasPressedThisFrame) split = true;
                if (kb.eKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame) merge = true;
            }

            float sens = config != null ? config.touchSensitivity : 0.012f;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                Vector2 pos = ts.primaryTouch.position.ReadValue();
                if (!_dragging) { _dragging = true; _touchStart = pos; _holdTimer = 0f; }
                Vector2 delta = (pos - _touchStart) * sens;
                rawMove += Vector2.ClampMagnitude(delta, 1f);
                if (delta.magnitude < 0.08f) _holdTimer += dt; else _holdTimer = 0f;
                if (_holdTimer > 0.35f) brake = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Vector2 pos = Mouse.current.position.ReadValue();
                if (!_dragging) { _dragging = true; _touchStart = pos; _holdTimer = 0f; }
                Vector2 delta = (pos - _touchStart) * sens;
                rawMove += Vector2.ClampMagnitude(delta, 1f);
                if (delta.magnitude < 0.08f) _holdTimer += dt; else _holdTimer = 0f;
                if (_holdTimer > 0.35f) brake = true;
            }
            else
            {
                _dragging = false;
                _holdTimer = 0f;
            }

            rawMove = Vector2.ClampMagnitude(rawMove, 1f);
            float smooth = config != null ? config.inputSmoothing : 14f;
            _smoothedMove = mode == ControlMode.SmoothedTarget
                ? Vector2.Lerp(_smoothedMove, rawMove, Mathf.Clamp01(dt * smooth))
                : rawMove;

            Current = new JellyInputFrame
            {
                Move = _smoothedMove,
                Brake = brake,
                SplitPressed = split,
                MergePressed = merge
            };
        }

        private void Update() => SampleInput(Time.deltaTime);
    }

    internal static class KeyboardExt
    {
        public static bool LeftShiftKeyPressed(this Keyboard kb) => kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
    }
}

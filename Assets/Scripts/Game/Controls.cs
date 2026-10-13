using PacMan.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PacMan.Game
{
    /// <summary>Keyboard (arrows/WASD), gamepad (d-pad/left stick) and, in menus, mouse input.</summary>
    public static class Controls
    {
        static Dir lastPressed = Dir.None;

        /// <summary>The direction to steer: the most recently pressed one while it is held, else any held one.</summary>
        public static Dir Direction()
        {
            foreach (var d in DirExtensions.Priority)
                if (Pressed(d)) lastPressed = d;
            if (lastPressed != Dir.None && Held(lastPressed)) return lastPressed;
            foreach (var d in DirExtensions.Priority)
                if (Held(d)) return d;
            return Dir.None;
        }

        static Dir menuHeld = Dir.None;
        static float menuRepeat;

        /// <summary>Menu navigation: fires once on press, then repeats while the direction is held.</summary>
        public static Dir MenuDirection()
        {
            var held = Dir.None;
            foreach (var d in DirExtensions.Priority)
                if (Held(d)) held = d;
            if (held != menuHeld)
            {
                menuHeld = held;
                menuRepeat = 0.4f;
                return held;
            }
            if (held == Dir.None) return Dir.None;
            menuRepeat -= Time.unscaledDeltaTime;
            if (menuRepeat > 0f) return Dir.None;
            menuRepeat = 0.09f;
            return held;
        }

        /// <summary>Mouse position in GUI coordinates (origin top-left), or null without a mouse.</summary>
        public static Vector2? Pointer
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var m = Mouse.current;
                if (m == null) return null;
                var p = m.position.ReadValue();
                return new Vector2(p.x, Screen.height - p.y);
#else
                var p = Input.mousePosition;
                return new Vector2(p.x, Screen.height - p.y);
#endif
            }
        }

#if ENABLE_INPUT_SYSTEM
        public static bool PointerMoved => Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.5f;
        public static bool PointerClicked => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        public static bool ConfirmPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                || (g != null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame));
        }

        public static bool CancelPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame))
                || (g != null && (g.buttonEast.wasPressedThisFrame || g.selectButton.wasPressedThisFrame));
        }

        static bool Held(Dir d)
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            switch (d)
            {
                case Dir.Up: return (k != null && (k.upArrowKey.isPressed || k.wKey.isPressed)) || (g != null && (g.dpad.up.isPressed || g.leftStick.y.ReadValue() > 0.5f));
                case Dir.Down: return (k != null && (k.downArrowKey.isPressed || k.sKey.isPressed)) || (g != null && (g.dpad.down.isPressed || g.leftStick.y.ReadValue() < -0.5f));
                case Dir.Left: return (k != null && (k.leftArrowKey.isPressed || k.aKey.isPressed)) || (g != null && (g.dpad.left.isPressed || g.leftStick.x.ReadValue() < -0.5f));
                case Dir.Right: return (k != null && (k.rightArrowKey.isPressed || k.dKey.isPressed)) || (g != null && (g.dpad.right.isPressed || g.leftStick.x.ReadValue() > 0.5f));
                default: return false;
            }
        }

        static bool Pressed(Dir d)
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            switch (d)
            {
                case Dir.Up: return (k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame)) || (g != null && g.dpad.up.wasPressedThisFrame);
                case Dir.Down: return (k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)) || (g != null && g.dpad.down.wasPressedThisFrame);
                case Dir.Left: return (k != null && (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame)) || (g != null && g.dpad.left.wasPressedThisFrame);
                case Dir.Right: return (k != null && (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame)) || (g != null && g.dpad.right.wasPressedThisFrame);
                default: return false;
            }
        }

        public static bool StartPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                || (g != null && (g.startButton.wasPressedThisFrame || g.buttonSouth.wasPressedThisFrame));
        }

        public static bool PausePressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.pKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)) || (g != null && g.startButton.wasPressedThisFrame);
        }

        public static bool BackPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.escapeKey.wasPressedThisFrame || k.qKey.wasPressedThisFrame)) || (g != null && g.selectButton.wasPressedThisFrame);
        }

        public static bool CameraPressed()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && k.cKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame);
        }

        public static bool MutePressed()
        {
            var k = Keyboard.current;
            return k != null && k.mKey.wasPressedThisFrame;
        }
#else
        public static bool PointerMoved => Input.GetAxisRaw("Mouse X") != 0f || Input.GetAxisRaw("Mouse Y") != 0f;
        public static bool PointerClicked => Input.GetMouseButtonDown(0);
        public static bool ConfirmPressed() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
        public static bool CancelPressed() => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);

        static bool Held(Dir d)
        {
            switch (d)
            {
                case Dir.Up: return Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
                case Dir.Down: return Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
                case Dir.Left: return Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
                case Dir.Right: return Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
                default: return false;
            }
        }

        static bool Pressed(Dir d)
        {
            switch (d)
            {
                case Dir.Up: return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
                case Dir.Down: return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
                case Dir.Left: return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
                case Dir.Right: return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
                default: return false;
            }
        }

        public static bool StartPressed() => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
        public static bool PausePressed() => Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape);
        public static bool BackPressed() => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Q);
        public static bool CameraPressed() => Input.GetKeyDown(KeyCode.C);
        public static bool MutePressed() => Input.GetKeyDown(KeyCode.M);
#endif
    }
}

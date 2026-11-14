using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MountainRemover
{
    public enum K { W, A, S, D, Up, Down, Left, Right, Space, Shift, E, B, Q, M, One, Two, Escape }

    /// <summary>
    /// Reads the keyboard and mouse through whichever input backend the project has enabled,
    /// so the game works with the new Input System (Unity 6 default) and the old Input Manager.
    /// </summary>
    public static class In
    {
#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Controls.KeyControl Key(K k)
        {
            var kb = Keyboard.current;
            if (kb == null) return null;
            switch (k)
            {
                case K.W: return kb.wKey;
                case K.A: return kb.aKey;
                case K.S: return kb.sKey;
                case K.D: return kb.dKey;
                case K.Up: return kb.upArrowKey;
                case K.Down: return kb.downArrowKey;
                case K.Left: return kb.leftArrowKey;
                case K.Right: return kb.rightArrowKey;
                case K.Space: return kb.spaceKey;
                case K.Shift: return kb.leftShiftKey;
                case K.E: return kb.eKey;
                case K.B: return kb.bKey;
                case K.Q: return kb.qKey;
                case K.M: return kb.mKey;
                case K.One: return kb.digit1Key;
                case K.Two: return kb.digit2Key;
                case K.Escape: return kb.escapeKey;
            }
            return null;
        }

        public static bool Held(K k)
        {
            var c = Key(k);
            if (c == null) return false;
            if (k == K.Shift) return c.isPressed || Keyboard.current.rightShiftKey.isPressed;
            return c.isPressed;
        }

        public static bool Down(K k) { var c = Key(k); return c != null && c.wasPressedThisFrame; }
        public static bool MouseHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool MouseDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        /// <summary>Mouse movement this frame in pixels.</summary>
        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        public static float Scroll => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        static KeyCode Code(K k)
        {
            switch (k)
            {
                case K.W: return KeyCode.W;
                case K.A: return KeyCode.A;
                case K.S: return KeyCode.S;
                case K.D: return KeyCode.D;
                case K.Up: return KeyCode.UpArrow;
                case K.Down: return KeyCode.DownArrow;
                case K.Left: return KeyCode.LeftArrow;
                case K.Right: return KeyCode.RightArrow;
                case K.Space: return KeyCode.Space;
                case K.Shift: return KeyCode.LeftShift;
                case K.E: return KeyCode.E;
                case K.B: return KeyCode.B;
                case K.Q: return KeyCode.Q;
                case K.M: return KeyCode.M;
                case K.One: return KeyCode.Alpha1;
                case K.Two: return KeyCode.Alpha2;
                case K.Escape: return KeyCode.Escape;
            }
            return KeyCode.None;
        }

        public static bool Held(K k)
        {
            if (k == K.Shift) return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return Input.GetKey(Code(k));
        }

        public static bool Down(K k) => Input.GetKeyDown(Code(k));
        public static bool MouseHeld => Input.GetMouseButton(0);
        public static bool MouseDown => Input.GetMouseButtonDown(0);
        // The old "Mouse X/Y" axes report roughly a tenth of the pixel distance.
        public static Vector2 MouseDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        public static float Scroll => Input.mouseScrollDelta.y;
#endif
    }
}

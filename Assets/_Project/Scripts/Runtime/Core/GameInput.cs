using UnityEngine;
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TurtleBlaster
{
    /// <summary>
    /// Dual-axis control scheme (GDD 3.1), merged from keyboard, gamepad, and on-screen touch buttons.
    ///   Left hand  (vertical axis):   Up = Pitch Up / Thrust    Down = Pitch Down / Ground Slam
    ///   Space / South Button: Jump (hop over cones)
    ///   Right hand (horizontal axis): Left = Backflip (CCW)     Right = Frontflip (CW)
    /// Touch buttons (see TouchControls) write into the Touch* flags below.
    /// Powered by the Unity Input System (Keyboard.current / Gamepad.current).
    /// </summary>
    public static class GameInput
    {
        public static bool TouchUp, TouchDown, TouchLeft, TouchRight;

        /// <summary>When set, overrides all real input (used by automated tests / replays).</summary>
        public static bool UseOverride;
        public static bool OverrideUp, OverrideDown, OverrideLeft, OverrideRight;

        /// <summary>Background auto-play bot input while on the Main Menu (lower priority than UseOverride).</summary>
        public static bool UseAttract;
        public static bool AttractUp, AttractDown, AttractLeft, AttractRight, AttractJump;

        /// <summary>Jump (Space). Edge-triggered: true only for the frame it was pressed. Touch button sets TouchJump.</summary>
        public static bool TouchJump, OverrideJump, OverrideBack;
        public static bool JumpPressed => UseOverride ? OverrideJump : (UseAttract ? AttractJump : (TouchJump || ReadJumpPressed()));

        /// <summary>
        /// Back / Exit navigation: triggered by Android system Back gesture / Back button,
        /// an inward swipe from the left or right screen edge, Keyboard (Escape / Backspace),
        /// or Gamepad (East / Select).
        /// </summary>
        public static bool BackPressed
        {
            get
            {
                if (UseOverride)
                {
                    bool v = OverrideBack;
                    OverrideBack = false;
                    return v;
                }
                return ReadBackKeyPressed() || ReadEdgeSwipeBack();
            }
        }

        static bool swipeTracking;
        static int swipeSide; // -1 = left edge, +1 = right edge
        static Vector2 swipeStartPos;
        static float swipeStartTime;

        public static bool Up => UseOverride ? OverrideUp : (UseAttract ? AttractUp : (TouchUp || ReadUp()));
        public static bool Down => UseOverride ? OverrideDown : (UseAttract ? AttractDown : (TouchDown || ReadDown()));
        public static bool Left => UseOverride ? OverrideLeft : (UseAttract ? AttractLeft : (TouchLeft || ReadLeft()));
        public static bool Right => UseOverride ? OverrideRight : (UseAttract ? AttractRight : (TouchRight || ReadRight()));

        /// <summary>+1 = pitch up / thrust, -1 = pitch down / slam.</summary>
        public static float Pitch => (Up ? 1f : 0f) - (Down ? 1f : 0f);

        /// <summary>+1 = backflip (counter-clockwise), -1 = frontflip (clockwise).</summary>
        public static float Flip => (Left ? 1f : 0f) - (Right ? 1f : 0f);

        public static void ClearTouch() { TouchJump = false; TouchUp = TouchDown = TouchLeft = TouchRight = false; swipeTracking = false; }
        public static void ClearAttract() { UseAttract = false; AttractUp = AttractDown = AttractLeft = AttractRight = AttractJump = false; }
        public static void ClearOverride() { UseOverride = false; OverrideUp = OverrideDown = OverrideLeft = OverrideRight = OverrideJump = OverrideBack = false; }

        static bool ReadBackKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonEast.wasPressedThisFrame || gp.selectButton.wasPressedThisFrame)) return true;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);
#else
            return false;
#endif
        }

        static bool ReadEdgeSwipeBack()
        {
            if (TouchUp || TouchDown || TouchLeft || TouchRight || TouchJump)
            {
                swipeTracking = false;
                return false;
            }

            float sw = Screen.width;
            if (sw <= 0f) return false;

            bool pressedThisFrame = false;
            bool isHeld = false;
            Vector2 pos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                pressedThisFrame = ts.primaryTouch.press.wasPressedThisFrame;
                isHeld = true;
                pos = ts.primaryTouch.position.ReadValue();
            }
            else
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.isPressed)
                {
                    pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
                    isHeld = true;
                    pos = mouse.position.ReadValue();
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                pressedThisFrame = t.phase == TouchPhase.Began;
                isHeld = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                pos = t.position;
            }
            else if (Input.GetMouseButton(0))
            {
                pressedThisFrame = Input.GetMouseButtonDown(0);
                isHeld = true;
                pos = Input.mousePosition;
            }
#endif

            if (pressedThisFrame)
            {
                float edgeThreshold = sw * 0.065f; // outer 6.5% of left or right screen edge
                if (pos.x <= edgeThreshold)
                {
                    swipeTracking = true;
                    swipeSide = -1;
                    swipeStartPos = pos;
                    swipeStartTime = Time.unscaledTime;
                }
                else if (pos.x >= sw - edgeThreshold)
                {
                    swipeTracking = true;
                    swipeSide = 1;
                    swipeStartPos = pos;
                    swipeStartTime = Time.unscaledTime;
                }
                else
                {
                    swipeTracking = false;
                }
            }
            else if (swipeTracking && isHeld)
            {
                if (Time.unscaledTime - swipeStartTime > 0.5f)
                {
                    swipeTracking = false;
                    return false;
                }
                float inwardDx = swipeSide < 0 ? (pos.x - swipeStartPos.x) : (swipeStartPos.x - pos.x);
                float dy = Mathf.Abs(pos.y - swipeStartPos.y);
                if (inwardDx >= sw * 0.08f && inwardDx > dy * 1.4f)
                {
                    swipeTracking = false;
                    return true;
                }
            }
            else if (!isHeld)
            {
                swipeTracking = false;
            }
            return false;
        }

        static bool ReadJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame)) return true;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        static bool ReadUp()
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.upArrowKey.isPressed || kb.wKey.isPressed)) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.dpad.up.isPressed || gp.leftStick.up.isPressed || gp.rightTrigger.isPressed)) return true;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
#else
            return false;
#endif
        }

        static bool ReadDown()
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.downArrowKey.isPressed || kb.sKey.isPressed)) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.dpad.down.isPressed || gp.leftStick.down.isPressed || gp.leftTrigger.isPressed)) return true;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
#else
            return false;
#endif
        }

        static bool ReadLeft()
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.leftArrowKey.isPressed || kb.aKey.isPressed)) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.dpad.left.isPressed || gp.leftStick.left.isPressed || gp.rightStick.left.isPressed)) return true;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
#else
            return false;
#endif
        }

        static bool ReadRight()
        {
#if ENABLE_INPUT_SYSTEM && HAS_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.rightArrowKey.isPressed || kb.dKey.isPressed)) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.dpad.right.isPressed || gp.leftStick.right.isPressed || gp.rightStick.right.isPressed)) return true;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
#else
            return false;
#endif
        }
    }
}

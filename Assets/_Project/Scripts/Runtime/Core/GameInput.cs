using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// Dual-axis control scheme (GDD 3.1), merged from keyboard and on-screen touch buttons.
    ///   Left hand  (vertical axis):   Up = Pitch Up / Thrust    Down = Pitch Down / Ground Slam
    ///   Space: Jump (hop over cones)
    ///   Right hand (horizontal axis): Left = Backflip (CCW)     Right = Frontflip (CW)
    /// Touch buttons (see TouchControls) write into the Touch* flags below.
    /// To use the new Input System or gamepads, only this class needs changing.
    /// </summary>
    public static class GameInput
    {
        public static bool TouchUp, TouchDown, TouchLeft, TouchRight;

        /// <summary>When set, overrides all real input (used by automated tests / replays).</summary>
        public static bool UseOverride;
        public static bool OverrideUp, OverrideDown, OverrideLeft, OverrideRight;

        /// <summary>Jump (Space). Edge-triggered: true only for the frame it was pressed. Touch button sets TouchJump.</summary>
        public static bool TouchJump, OverrideJump;
        public static bool JumpPressed => UseOverride ? OverrideJump : (TouchJump || Input.GetKeyDown(KeyCode.Space));

        public static bool Up => UseOverride ? OverrideUp : (TouchUp || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W));
        public static bool Down => UseOverride ? OverrideDown : (TouchDown || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S));
        public static bool Left => UseOverride ? OverrideLeft : (TouchLeft || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A));
        public static bool Right => UseOverride ? OverrideRight : (TouchRight || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D));

        /// <summary>+1 = pitch up / thrust, -1 = pitch down / slam.</summary>
        public static float Pitch => (Up ? 1f : 0f) - (Down ? 1f : 0f);

        /// <summary>+1 = backflip (counter-clockwise), -1 = frontflip (clockwise).</summary>
        public static float Flip => (Left ? 1f : 0f) - (Right ? 1f : 0f);

        public static void ClearTouch() { TouchJump = false; TouchUp = TouchDown = TouchLeft = TouchRight = false; }
        public static void ClearOverride() { UseOverride = false; OverrideUp = OverrideDown = OverrideLeft = OverrideRight = OverrideJump = false; }
    }
}

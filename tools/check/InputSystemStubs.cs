// Minimal stand-in for the Unity Input System, used ONLY by the local compile
// check in scripts/smoke-compile.ps1. It is never compiled into the game.
//
// Why this exists: the real com.unity.inputsystem package ships as C# source,
// not a DLL, so the check has to compile those sources to type-check the
// keyboard and mouse code. Those sources cannot be compiled outside Unity --
// they produce 20 hard errors (CS0636 FieldOffset, CS0182 attribute argument,
// CS0122 NativeInputEvent inaccessible) that need Unity's native binding
// defines and assembly layout.
//
// Those 20 declaration-phase errors are not cosmetic. When a compilation
// contains any declaration-phase error, Roslyn does not bind method bodies in
// other files, so every body-level error in our own code is silently dropped.
// That is how CS0120 in DayNightCycle and CS1503 in PlayableNpc both reached CI
// while the local check reported zero actionable errors.
//
// This file declares exactly the API surface the playable layer uses and
// compiles without errors, so the check can actually see method bodies.
using UnityEngine;

// PlayableCameraRig only has "using UnityEngine", and that is what the real
// package relies on too: the Input System extension methods live in the
// UnityEngine namespace, not next to the controls themselves. Putting them in
// UnityEngine.InputSystem made them invisible to call sites.
namespace UnityEngine
{
    public static class InputControlExtensions
    {
        public static UnityEngine.Vector2 ReadValue(this UnityEngine.Vector2 value) => value;
        public static UnityEngine.Vector2 ReadValue(
            this UnityEngine.InputSystem.Vector2Control control) => UnityEngine.Vector2.zero;
        public static float ReadValue(this UnityEngine.InputSystem.ButtonControl control) => 0f;
        public static UnityEngine.Vector2 ReadValue(this UnityEngine.InputSystem.Mouse control)
            => UnityEngine.Vector2.zero;
    }
}

namespace UnityEngine.InputSystem
{
    public enum Key
    {
        None,
        A, B, C, D, E, F, G, H, I, J, K, L, M,
        N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit0, Digit1, Digit2, Digit3, Digit4,
        Digit5, Digit6, Digit7, Digit8, Digit9,
        Space, Enter, Escape, Tab, Backspace, Delete,
        LeftArrow, RightArrow, UpArrow, DownArrow,
        LeftShift, RightShift, LeftCtrl, RightCtrl, LeftAlt, RightAlt
    }

    public class ButtonControl
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
        public bool wasReleasedThisFrame => false;
    }

    public class KeyControl : ButtonControl
    {
        public Key key => Key.None;
    }

    public class Keyboard
    {
        public static Keyboard current => null;

        public KeyControl this[Key key] => null;

        public KeyControl leftShiftKey => null;
        public KeyControl rightShiftKey => null;
        public KeyControl leftCtrlKey => null;
        public KeyControl rightCtrlKey => null;
        public KeyControl leftAltKey => null;
        public KeyControl rightAltKey => null;
        public KeyControl spaceKey => null;
        public KeyControl enterKey => null;
        public KeyControl escapeKey => null;
    }

    public class MouseButton
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
    }

    /// <summary>Scroll is a two-axis control, not a plain float.</summary>
    public class Vector2Control
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
    }

    public class Mouse
    {
        public static Mouse current => null;

        public Vector2 delta => Vector2.zero;
        public Vector2 position => Vector2.zero;
        public Vector2Control scroll => null;

        public MouseButton leftButton => null;
        public MouseButton rightButton => null;
        public MouseButton middleButton => null;
    }

    public class GamepadButton
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
    }

    public class Gamepad
    {
        public static Gamepad current => null;
        public GamepadButton buttonSouth => null;
        public GamepadButton buttonWest => null;
    }
}

namespace Megame.Client
{
    /// <summary>
    /// Stand-in for the networked client, which lives in GameClient.cs and
    /// cannot be compiled outside the generated protobuf. PlayableHUD only
    /// reads these five members to render the connection line.
    /// </summary>
    public class GameClient
    {
        public static GameClient Instance => null;

        public bool autoConnect => false;
        public bool IsConnected => false;
        public ulong LocalPlayerId => 0ul;
        public ulong ServerTick => 0ul;
    }
}
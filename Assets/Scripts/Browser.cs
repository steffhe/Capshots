using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// Things only the browser build needs. In other builds they do nothing or fall back to Unity's own state.
// The JavaScript side is Assets/Plugins/WebGL/Browser.jslib.
public static class Browser
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int IsBrowserPointerLocked();
    [DllImport("__Internal")] private static extern void SetBrowserLeaveWarning(int enabled);
    [DllImport("__Internal")] private static extern int IsBrowserTouchDevice();

    private static int touchDevice = -1;

    public const bool IsBrowser = true;
    public static bool IsPointerLocked => IsBrowserPointerLocked() != 0;
    public static void SetLeaveWarning(bool enabled) => SetBrowserLeaveWarning(enabled ? 1 : 0);

    // A phone or tablet: the main pointer is a finger rather than a mouse
    public static bool IsTouchDevice
    {
        get
        {
            if (touchDevice < 0) touchDevice = IsBrowserTouchDevice();
            return touchDevice != 0;
        }
    }
#else
    public const bool IsBrowser = false;
    public static bool IsPointerLocked => Cursor.lockState == CursorLockMode.Locked;
    public static void SetLeaveWarning(bool enabled) { }
    public static bool IsTouchDevice => false;
#endif
}

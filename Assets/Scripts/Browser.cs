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
    [DllImport("__Internal")] private static extern int GetBrowserPointerType();
    [DllImport("__Internal")] private static extern void ClearBrowserUrlQuery();
    [DllImport("__Internal")] private static extern void CopyBrowserText(string text);

    public static void ClearUrlQuery() => ClearBrowserUrlQuery();
    public static void CopyText(string text) => CopyBrowserText(text);

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

    public static int LastPointerType => GetBrowserPointerType();

    // Starts listening for the pointer type at launch, so a trackpad used in the menu already counts
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartPointerTracking() => GetBrowserPointerType();
#else
    public const bool IsBrowser = false;
    public static bool IsPointerLocked => Cursor.lockState == CursorLockMode.Locked;
    public static void SetLeaveWarning(bool enabled) { }
    public static bool IsTouchDevice => false;
    public static int LastPointerType => PointerNone;
    public static void ClearUrlQuery() { }
    public static void CopyText(string text) => GUIUtility.systemCopyBuffer = text;
#endif

    // Where the browser version is published, for invite links from the Windows version
    public const string PublishedUrl = "https://steffhe.github.io/Capshots/";

    // A link that joins this room code's match in the browser
    public static string InviteLink(string code)
    {
        string page = IsBrowser && !string.IsNullOrEmpty(Application.absoluteURL) ? Application.absoluteURL : PublishedUrl;
        return RoomCodes.InviteLink(page, code);
    }

    // The kind of pointer the player used last: a mouse or trackpad, or a finger or pen
    public const int PointerNone = 0, PointerMouse = 1, PointerTouch = 2;
}

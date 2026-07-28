using System.Runtime.InteropServices;
using System.Text;

namespace IllustratorTypeFlow;

internal static class NativeMethods
{
    internal const uint EventSystemForeground = 0x0003;
    internal const uint EventObjectFocus = 0x8005;
    internal const uint WineventOutOfContext = 0x0000;
    internal const uint WineventSkipOwnProcess = 0x0002;
    internal const int ObjidWindow = 0;
    internal const uint GcsCompStr = 0x0008;
    internal const uint WmImeControl = 0x0283;
    internal const nuint ImcGetOpenStatus = 0x0005;
    internal const nuint ImcSetOpenStatus = 0x0006;
    internal const uint WmQuit = 0x0012;
    internal const int WhKeyboardLl = 13;
    internal const int WhMouseLl = 14;
    internal const uint WmKeyDown = 0x0100;
    internal const uint WmSysKeyDown = 0x0104;
    internal const uint WmLButtonDown = 0x0201;
    internal const int VkEscape = 0x1B;
    internal const int VkReturn = 0x0D;
    internal const int VkControl = 0x11;
    internal const int VkMenu = 0x12;
    internal const int VkLControl = 0xA2;
    internal const int VkRControl = 0xA3;
    internal const int VkLMenu = 0xA4;
    internal const int VkRMenu = 0xA5;

    internal delegate void WinEventDelegate(
        nint hook, uint eventType, nint hwnd, int objectId, int childId,
        uint eventThread, uint eventTime);

    internal delegate nint HookProc(int code, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardHookData
    {
        internal uint VirtualKey;
        internal uint ScanCode;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseHookData
    {
        internal Point Position;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GuiThreadInfo
    {
        internal int cbSize;
        internal int flags;
        internal nint hwndActive;
        internal nint hwndFocus;
        internal nint hwndCapture;
        internal nint hwndMenuOwner;
        internal nint hwndMoveSize;
        internal nint hwndCaret;
        internal Rect rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll")]
    internal static extern nint SetWinEventHook(
        uint eventMin, uint eventMax, nint module, WinEventDelegate callback,
        uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    internal static extern nint SetWindowsHookEx(
        int hookId, HookProc callback, nint module, uint threadId);

    [DllImport("user32.dll")]
    internal static extern nint CallNextHookEx(nint hook, int code, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hook);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    internal static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    internal static extern short GetKeyState(int virtualKey);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern nint GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    internal static extern nint ActivateKeyboardLayout(nint keyboardLayout, uint flags);

    [DllImport("imm32.dll")]
    internal static extern nint ImmGetContext(nint hwnd);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ImmReleaseContext(nint hwnd, nint context);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ImmGetOpenStatus(nint context);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ImmSetOpenStatus(nint context, [MarshalAs(UnmanagedType.Bool)] bool open);

    [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
    internal static extern int ImmGetCompositionString(nint context, uint index, nint buffer, uint bufferLength);

    [DllImport("imm32.dll")]
    internal static extern nint ImmGetDefaultIMEWnd(nint hwnd);

    [DllImport("user32.dll")]
    internal static extern nint SendMessage(nint hwnd, uint message, nuint wParam, nint lParam);

    internal static string GetWindowClass(nint hwnd)
    {
        if (hwnd == 0)
            return "";

        var buffer = new StringBuilder(256);
        _ = GetClassName(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    internal static nint GetFocusedWindow(nint foreground, uint threadId)
    {
        var info = new GuiThreadInfo { cbSize = Marshal.SizeOf<GuiThreadInfo>() };
        return GetGUIThreadInfo(threadId, ref info) && info.hwndFocus != 0
            ? info.hwndFocus
            : foreground;
    }
}

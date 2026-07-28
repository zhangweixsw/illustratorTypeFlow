using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using DrawingPoint = System.Windows.Point;

namespace IllustratorTypeFlow;

public sealed class CanvasHeuristicMonitor : IDisposable
{
    private static readonly HashSet<uint> NonTypeToolKeys =
    [
        'V', 'A', 'P', 'B', 'N', 'M', 'L', 'I', 'K', 'G', 'R', 'O', 'S', 'E', 'C', 'H', 'J', 'Q', 'W', 'Y', 'U'
    ];

    private readonly FileLogger logger;
    private readonly CanvasHeuristicStateMachine machine = new();
    private readonly NativeMethods.HookProc keyboardCallback;
    private readonly NativeMethods.HookProc mouseCallback;
    private readonly nint keyboardHook;
    private readonly nint mouseHook;
    private readonly object sync = new();
    private volatile bool uiInputFocused;
    private NativeMethods.Point lastCanvasClick;
    private uint lastCanvasClickTime;

    public CanvasHeuristicMonitor(FileLogger logger)
    {
        this.logger = logger;
        keyboardCallback = KeyboardHook;
        mouseCallback = MouseHook;
        keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl, keyboardCallback, 0, 0);
        mouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhMouseLl, mouseCallback, 0, 0);

        if (keyboardHook == 0 || mouseHook == 0)
            logger.Warn("无插件画布监听钩子安装不完整");
    }

    public event EventHandler? StateChanged;

    public CanvasIntent State
    {
        get
        {
            lock (sync)
                return machine.State;
        }
    }

    public void SetUiInputFocused(bool focused) => uiInputFocused = focused;

    public void SetManualEditing(bool editing)
    {
        lock (sync)
        {
            if (machine.SetManual(editing))
                RaiseChanged();
        }
    }

    public void ResetIfOutsideIllustrator()
    {
        if (IsIllustratorForeground())
            return;

        lock (sync)
        {
            if (machine.Reset())
                RaiseChanged();
        }
    }

    private nint KeyboardHook(int code, nuint wParam, nint lParam)
    {
        if (code >= 0 &&
            (wParam == NativeMethods.WmKeyDown || wParam == NativeMethods.WmSysKeyDown) &&
            IsIllustratorForeground())
        {
            var data = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(lParam);
            var ctrl = IsDown(NativeMethods.VkControl) ||
                       IsDown(NativeMethods.VkLControl) ||
                       IsDown(NativeMethods.VkRControl);
            var alt = IsDown(NativeMethods.VkMenu) ||
                      IsDown(NativeMethods.VkLMenu) ||
                      IsDown(NativeMethods.VkRMenu);

            var shortcut = MapShortcut(data.VirtualKey, ctrl, alt);
            lock (sync)
            {
                if (machine.OnShortcut(shortcut, uiInputFocused))
                    RaiseChanged();
            }
        }

        return NativeMethods.CallNextHookEx(keyboardHook, code, wParam, lParam);
    }

    private nint MouseHook(int code, nuint wParam, nint lParam)
    {
        if (code >= 0 && wParam == NativeMethods.WmLButtonDown && IsIllustratorForeground())
        {
            var data = Marshal.PtrToStructure<NativeMethods.MouseHookData>(lParam);
            // Toolbars, menu bars and status bars are outside the canvas
            // geometry and need no potentially slow UIA lookup. Clear the
            // canvas-editing state immediately so shortcuts work on mouse-up.
            if (!IsInsideCanvasGeometry(data.Position))
            {
                lock (sync)
                {
                    if (machine.OnNonCanvasClick(isTextInput: false))
                        RaiseChanged();
                }
            }
            else
            {
                _ = Task.Run(() => ProcessClick(data.Position, data.Time));
            }
        }

        return NativeMethods.CallNextHookEx(mouseHook, code, wParam, lParam);
    }

    private void ProcessClick(NativeMethods.Point point, uint time)
    {
        try
        {
            if (!IsIllustratorForeground())
                return;

            var hit = InspectPoint(point);
            lock (sync)
            {
                if (!hit.IsCanvas)
                {
                    if (machine.OnNonCanvasClick(hit.IsTextInput))
                        RaiseChanged();
                    return;
                }

                var doubleClick =
                    time - lastCanvasClickTime <= NativeMethods.GetDoubleClickTime() &&
                    Math.Abs(point.X - lastCanvasClick.X) <= 5 &&
                    Math.Abs(point.Y - lastCanvasClick.Y) <= 5;
                lastCanvasClick = point;
                lastCanvasClickTime = time;

                if (machine.OnCanvasClick(doubleClick))
                    RaiseChanged();
            }
        }
        catch (Exception exception)
        {
            logger.Error("判断画布点击失败", exception);
        }
    }

    private static HitResult InspectPoint(NativeMethods.Point point)
    {
        if (!IsInsideCanvasGeometry(point))
            return new(false, false);

        try
        {
            var element = AutomationElement.FromPoint(new DrawingPoint(point.X, point.Y));
            if (element is null)
                return new(true, false);

            var type = element.Current.ControlType;
            // Illustrator exposes the actual drawing surface as a UIA Document.
            // Treating every Document as an input field makes all canvas clicks
            // look like CEP/UXP clicks and prevents the T + click fallback from
            // ever entering text mode. Real panel inputs normally expose Edit or
            // ComboBox; the separate focus classifier handles unusual plug-ins.
            var isTextInput = type is not null &&
                              (type == ControlType.Edit ||
                               type == ControlType.ComboBox);
            if (isTextInput || type == ControlType.Spinner)
                return new(false, isTextInput);

            if (type == ControlType.Button ||
                type == ControlType.MenuItem ||
                type == ControlType.TabItem ||
                type == ControlType.Slider ||
                type == ControlType.List ||
                type == ControlType.Tree)
                return new(false, false);

            // Illustrator's drawing surface, artboards and live artwork are
            // inconsistently exposed as Pane or Custom elements (often named).
            // Do not reject those generic types. Actual CEP/UXP controls are
            // caught above by their interactive UIA types, while the focus
            // classifier continues to handle panel text inputs.
        }
        catch (ElementNotAvailableException)
        {
            return new(true, false);
        }

        return new(true, false);
    }

    private static bool IsInsideCanvasGeometry(NativeMethods.Point point)
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (!NativeMethods.GetWindowRect(foreground, out var bounds))
            return false;

        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        return
            point.X >= bounds.Left + Math.Min(72, width / 10) &&
            point.X <= bounds.Right - Math.Min(48, width / 15) &&
            point.Y >= bounds.Top + Math.Min(92, height / 8) &&
            point.Y <= bounds.Bottom - Math.Min(40, height / 12);
    }

    private static CanvasShortcut MapShortcut(uint key, bool ctrl, bool alt)
    {
        if (key == NativeMethods.VkEscape)
            return CanvasShortcut.Escape;
        if (key == NativeMethods.VkReturn && ctrl)
            return CanvasShortcut.CommitAndExit;
        if (ctrl || alt)
            return CanvasShortcut.Other;
        if (key == 'T')
            return CanvasShortcut.TypeTool;
        return NonTypeToolKeys.Contains(key) ? CanvasShortcut.NonTypeTool : CanvasShortcut.Other;
    }

    private static bool IsDown(int key) => (NativeMethods.GetKeyState(key) & 0x8000) != 0;

    private static bool IsIllustratorForeground()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
            return false;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName.Equals("Illustrator", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void RaiseChanged()
    {
        logger.Info($"无插件画布状态：{machine.State.IsEditing}；{machine.State.Reason}");
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (keyboardHook != 0)
            _ = NativeMethods.UnhookWindowsHookEx(keyboardHook);
        if (mouseHook != 0)
            _ = NativeMethods.UnhookWindowsHookEx(mouseHook);
    }

    private sealed record HitResult(bool IsCanvas, bool IsTextInput);
}

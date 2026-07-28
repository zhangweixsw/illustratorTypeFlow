using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace IllustratorTypeFlow;

public sealed class FocusMonitor : IDisposable
{
    private readonly FileLogger logger;
    private readonly NativeMethods.WinEventDelegate callback;
    private readonly nint foregroundHook;
    private readonly nint focusHook;

    public FocusMonitor(FileLogger logger)
    {
        this.logger = logger;
        callback = OnWinEvent;
        foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground, NativeMethods.EventSystemForeground,
            0, callback, 0, 0,
            NativeMethods.WineventOutOfContext | NativeMethods.WineventSkipOwnProcess);
        focusHook = NativeMethods.SetWinEventHook(
            NativeMethods.EventObjectFocus, NativeMethods.EventObjectFocus,
            0, callback, 0, 0,
            NativeMethods.WineventOutOfContext | NativeMethods.WineventSkipOwnProcess);
    }

    public event EventHandler? FocusChanged;

    public FocusInfo Capture()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        var threadId = NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
        var isIllustrator = IsIllustratorProcess((int)processId);
        var focusHandle = NativeMethods.GetFocusedWindow(foreground, threadId);

        var controlKind = ControlKind.Unknown;
        var name = "";
        var automationId = "";
        var className = NativeMethods.GetWindowClass(focusHandle);
        var frameworkId = "";
        var ancestors = new List<string>();

        if (isIllustrator)
        {
            try
            {
                var element = AutomationElement.FocusedElement;
                if (element is not null)
                {
                    name = SafeGet(() => element.Current.Name);
                    automationId = SafeGet(() => element.Current.AutomationId);
                    className = SafeGet(() => element.Current.ClassName, className);
                    frameworkId = SafeGet(() => element.Current.FrameworkId);
                    controlKind = MapControlType(element.Current.ControlType);
                    CaptureAncestors(element, ancestors);
                }
            }
            catch (ElementNotAvailableException)
            {
                // Focus changed while reading it; the debounce will retry.
            }
            catch (COMException exception)
            {
                logger.Error("读取 UI Automation 焦点失败", exception);
            }
        }

        return new FocusInfo(
            isIllustrator, (int)processId, foreground, focusHandle, controlKind,
            name, automationId, className, frameworkId, ancestors);
    }

    private void OnWinEvent(
        nint hook, uint eventType, nint hwnd, int objectId, int childId,
        uint eventThread, uint eventTime)
    {
        if (eventType == NativeMethods.EventObjectFocus &&
            objectId != NativeMethods.ObjidWindow && objectId >= 0)
            return;

        FocusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsIllustratorProcess(int processId)
    {
        if (processId <= 0)
            return false;

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName.Equals("Illustrator", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static ControlKind MapControlType(ControlType type)
    {
        if (type == ControlType.Edit)
            return ControlKind.Edit;
        if (type == ControlType.Document)
            return ControlKind.Document;
        if (type == ControlType.Spinner)
            return ControlKind.Spinner;
        return ControlKind.Unknown;
    }

    private static void CaptureAncestors(AutomationElement element, ICollection<string> target)
    {
        var walker = TreeWalker.ControlViewWalker;
        var current = element;
        for (var i = 0; i < 8; i++)
        {
            current = walker.GetParent(current);
            if (current is null)
                break;

            var name = SafeGet(() => current.Current.Name);
            var className = SafeGet(() => current.Current.ClassName);
            if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(className))
                target.Add($"{name}[{className}]");
        }
    }

    private static string SafeGet(Func<string> getter, string fallback = "")
    {
        try
        {
            return getter() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public void Dispose()
    {
        if (foregroundHook != 0)
            _ = NativeMethods.UnhookWinEvent(foregroundHook);
        if (focusHook != 0)
            _ = NativeMethods.UnhookWinEvent(focusHook);
    }
}


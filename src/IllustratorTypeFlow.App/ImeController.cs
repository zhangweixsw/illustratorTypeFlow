namespace IllustratorTypeFlow;

public sealed class ImeController
{
    private readonly FileLogger logger;
    private ImeSnapshot? original;
    private nint lastIllustratorFocus;
    private bool? lastRequestedOpen;

    public ImeController(FileLogger logger)
    {
        this.logger = logger;
    }

    public bool Apply(FocusInfo focus, bool open)
    {
        if (focus.FocusHandle == 0)
            return false;

        if (original is null)
            original = Capture(focus);

        lastIllustratorFocus = focus.FocusHandle;
        if (IsComposing(focus.FocusHandle))
        {
            logger.Info("输入法正在组词，延迟状态切换");
            return false;
        }

        if (lastRequestedOpen == open && GetOpenStatus(focus.FocusHandle) == open)
            return true;

        var changed = SetOpenStatus(focus.FocusHandle, open);
        if (changed)
        {
            lastRequestedOpen = open;
            logger.Info(open ? "输入法切换为中文" : "输入法切换为英文");
        }
        else
        {
            logger.Warn($"无法设置 IME 状态，窗口类：{focus.ClassName}");
        }

        return changed;
    }

    public void Restore()
    {
        if (original is null)
            return;

        var target = lastIllustratorFocus != 0 ? lastIllustratorFocus : original.FocusHandle;
        if (target != 0 && !IsComposing(target))
        {
            _ = SetOpenStatus(target, original.Open);
            if (original.KeyboardLayout != 0)
                _ = NativeMethods.ActivateKeyboardLayout(original.KeyboardLayout, 0);
        }

        logger.Info("已恢复进入 Illustrator 前的输入法状态");
        original = null;
        lastRequestedOpen = null;
        lastIllustratorFocus = 0;
    }

    public static bool IsComposing(nint hwnd)
    {
        var context = NativeMethods.ImmGetContext(hwnd);
        if (context == 0)
            return false;
        try
        {
            return NativeMethods.ImmGetCompositionString(context, NativeMethods.GcsCompStr, 0, 0) > 0;
        }
        finally
        {
            _ = NativeMethods.ImmReleaseContext(hwnd, context);
        }
    }

    private static ImeSnapshot Capture(FocusInfo focus)
    {
        var threadId = NativeMethods.GetWindowThreadProcessId(focus.FocusHandle, out _);
        return new ImeSnapshot(
            focus.FocusHandle,
            NativeMethods.GetKeyboardLayout(threadId),
            GetOpenStatus(focus.FocusHandle));
    }

    private static bool GetOpenStatus(nint hwnd)
    {
        var context = NativeMethods.ImmGetContext(hwnd);
        if (context != 0)
        {
            try
            {
                return NativeMethods.ImmGetOpenStatus(context);
            }
            finally
            {
                _ = NativeMethods.ImmReleaseContext(hwnd, context);
            }
        }

        var imeWindow = NativeMethods.ImmGetDefaultIMEWnd(hwnd);
        return imeWindow != 0 &&
               NativeMethods.SendMessage(
                   imeWindow, NativeMethods.WmImeControl,
                   NativeMethods.ImcGetOpenStatus, 0) != 0;
    }

    private static bool SetOpenStatus(nint hwnd, bool open)
    {
        var context = NativeMethods.ImmGetContext(hwnd);
        if (context != 0)
        {
            try
            {
                if (NativeMethods.ImmSetOpenStatus(context, open))
                    return true;
            }
            finally
            {
                _ = NativeMethods.ImmReleaseContext(hwnd, context);
            }
        }

        var imeWindow = NativeMethods.ImmGetDefaultIMEWnd(hwnd);
        if (imeWindow == 0)
            return false;

        _ = NativeMethods.SendMessage(
            imeWindow, NativeMethods.WmImeControl,
            NativeMethods.ImcSetOpenStatus, open ? 1 : 0);
        return GetOpenStatus(hwnd) == open;
    }

    private sealed record ImeSnapshot(nint FocusHandle, nint KeyboardLayout, bool Open);
}

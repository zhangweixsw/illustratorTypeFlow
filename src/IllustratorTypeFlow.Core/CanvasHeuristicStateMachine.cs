namespace IllustratorTypeFlow;

public sealed class CanvasHeuristicStateMachine
{
    public CanvasIntent State { get; private set; } =
        new(false, false, "等待文字工具");

    public bool OnShortcut(CanvasShortcut shortcut, bool uiInputFocused)
    {
        var next = shortcut switch
        {
            CanvasShortcut.Escape => new CanvasIntent(
                TypeToolArmed: State.TypeToolArmed,
                IsEditing: false,
                Reason: "Esc 退出画布文字编辑"),
            CanvasShortcut.CommitAndExit => new CanvasIntent(
                TypeToolArmed: State.TypeToolArmed,
                IsEditing: false,
                Reason: "提交并退出画布文字编辑"),
            CanvasShortcut.TypeTool when !State.IsEditing && !uiInputFocused => new CanvasIntent(
                TypeToolArmed: true,
                IsEditing: false,
                Reason: "文字工具快捷键已激活"),
            CanvasShortcut.NonTypeTool when !State.IsEditing && !uiInputFocused => new CanvasIntent(
                TypeToolArmed: false,
                IsEditing: false,
                Reason: "其他工具快捷键已激活"),
            _ => State
        };
        return Set(next);
    }

    public bool OnCanvasClick(bool isDoubleClick)
    {
        if (!State.TypeToolArmed && !State.IsEditing && !isDoubleClick)
            return false;

        return Set(new CanvasIntent(
            TypeToolArmed: State.TypeToolArmed,
            IsEditing: true,
            Reason: isDoubleClick
                ? "双击画布对象，推断进入文字编辑"
                : "文字工具点击画布，推断进入文字编辑"));
    }

    public bool OnNonCanvasClick(bool isTextInput)
    {
        if (isTextInput)
        {
            return Set(new CanvasIntent(
                TypeToolArmed: State.TypeToolArmed,
                IsEditing: false,
                Reason: "焦点进入界面文字输入框"));
        }

        return Set(new CanvasIntent(
            TypeToolArmed: false,
            IsEditing: false,
            Reason: "点击非画布控件"));
    }

    public bool SetManual(bool editing) =>
        Set(new CanvasIntent(
            TypeToolArmed: editing || State.TypeToolArmed,
            IsEditing: editing,
            Reason: editing ? "用户手动设为画布文字编辑" : "用户手动退出画布文字编辑"));

    public bool Reset() =>
        Set(new CanvasIntent(false, false, "离开 Illustrator"));

    private bool Set(CanvasIntent next)
    {
        if (next == State)
            return false;
        State = next;
        return true;
    }
}

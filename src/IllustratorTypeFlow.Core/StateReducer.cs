namespace IllustratorTypeFlow;

public static class StateReducer
{
    public static CoordinatorDecision Decide(CoordinatorInput input)
    {
        if (!input.Enabled)
            return new(false, false, FocusKind.NonEditable, "程序已暂停");

        if (!input.IsIllustrator)
            return new(false, false, FocusKind.NonEditable, "离开 Illustrator，恢复原状态");

        var focus = input.Focus;
        if (input.PluginState == PluginState.CanvasTextEditing)
            focus = new(FocusKind.CanvasText, "画布文字编辑状态已激活");

        return new(
            ManageIllustrator: true,
            WantsChinese: focus.WantsChinese,
            EffectiveKind: focus.Kind,
            Reason: focus.Reason);
    }
}

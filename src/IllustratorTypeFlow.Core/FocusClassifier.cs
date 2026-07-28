using System.Text.RegularExpressions;

namespace IllustratorTypeFlow;

public sealed partial class FocusClassifier
{
    private static readonly string[] RenameTerms =
    [
        "图层", "画板", "layer", "artboard"
    ];

    private static readonly string[] NumericTerms =
    [
        "宽", "高", "位置", "坐标", "字号", "字体大小", "透明度", "角度", "缩放",
        "描边", "半径", "间距", "行距", "字距", "基线", "不透明",
        "width", "height", "position", "x:", "y:", "font size", "opacity",
        "angle", "scale", "stroke", "radius", "spacing", "leading", "tracking",
        "baseline", "zoom", "rotation", "rotate"
    ];

    private readonly IReadOnlyDictionary<string, FieldOverride> overrides;

    public FocusClassifier(IReadOnlyDictionary<string, FieldOverride>? overrides = null)
    {
        this.overrides = overrides ?? new Dictionary<string, FieldOverride>();
    }

    public ClassificationResult Classify(FocusInfo focus, PluginState pluginState)
    {
        if (!focus.IsIllustrator)
            return new(FocusKind.NonEditable, "前台应用不是 Illustrator");

        if (pluginState == PluginState.CanvasTextEditing)
            return new(FocusKind.CanvasText, "画布文字编辑状态已激活");

        if (overrides.TryGetValue(focus.Signature, out var fieldOverride))
        {
            return fieldOverride switch
            {
                FieldOverride.Text => new(FocusKind.PluginTextField, "用户规则：文字输入框"),
                FieldOverride.Numeric => new(FocusKind.NumericParameter, "用户规则：参数输入框"),
                _ => ClassifyAutomatically(focus)
            };
        }

        return ClassifyAutomatically(focus);
    }

    private static ClassificationResult ClassifyAutomatically(FocusInfo focus)
    {
        if (focus.ControlKind == ControlKind.Spinner)
            return new(FocusKind.NumericParameter, "UI Automation 控件类型为 Spinner");

        // UIA Document means a document surface/web document, not an active
        // text insertion point. Illustrator's canvas and entire CEP/UXP panels
        // commonly expose this type even when no caret exists. Only a real Edit
        // control (or an explicit user override handled above) may enable IME.
        if (focus.ControlKind != ControlKind.Edit)
            return new(FocusKind.NonEditable, "焦点不在可编辑控件");

        var searchable = string.Join(" ", focus.Name, focus.AutomationId, focus.ClassName,
            string.Join(" ", focus.Ancestors)).ToLowerInvariant();

        if (RenameTerms.Any(searchable.Contains))
            return new(FocusKind.LayerOrArtboardName, "图层或画板重命名控件");

        if (LooksNumeric(searchable))
            return new(FocusKind.NumericParameter, "参数名称或当前值呈数值特征");

        return new(FocusKind.PluginTextField, "Illustrator 内普通文字输入控件");
    }

    private static bool LooksNumeric(string searchable)
    {
        if (NumericTerms.Any(searchable.Contains))
            return true;

        return UnitPattern().IsMatch(searchable) || NumericOnlyPattern().IsMatch(searchable.Trim());
    }

    [GeneratedRegex(@"(?:^|\s)-?\d+(?:[.,]\d+)?\s*(?:px|pt|mm|cm|in|%|°|度)(?:\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnitPattern();

    [GeneratedRegex(@"^-?\d+(?:[.,]\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumericOnlyPattern();
}

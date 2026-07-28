namespace IllustratorTypeFlow;

public enum PluginState
{
    Unavailable,
    NotEditing,
    CanvasTextEditing
}

public enum FocusKind
{
    CanvasText,
    LayerOrArtboardName,
    PluginTextField,
    NumericParameter,
    NonEditable
}

public enum ControlKind
{
    Unknown,
    Edit,
    Document,
    Spinner
}

public enum FieldOverride
{
    None,
    Text,
    Numeric
}

public sealed record FocusInfo(
    bool IsIllustrator,
    int ProcessId,
    nint WindowHandle,
    nint FocusHandle,
    ControlKind ControlKind,
    string Name,
    string AutomationId,
    string ClassName,
    string FrameworkId,
    IReadOnlyList<string> Ancestors)
{
    public string Signature =>
        string.Join("|", AutomationId, Name, ClassName, FrameworkId,
            string.Join(">", Ancestors.Take(5)));
}

public sealed record ClassificationResult(FocusKind Kind, string Reason)
{
    public bool WantsChinese =>
        Kind is FocusKind.CanvasText or FocusKind.LayerOrArtboardName or FocusKind.PluginTextField;
}

public sealed record CoordinatorInput(
    bool Enabled,
    bool IsIllustrator,
    PluginState PluginState,
    ClassificationResult Focus);

public sealed record CoordinatorDecision(
    bool ManageIllustrator,
    bool WantsChinese,
    FocusKind EffectiveKind,
    string Reason);

public sealed record PluginMessage(
    int Protocol,
    string State,
    int Pid,
    long Timestamp);

public enum CanvasShortcut
{
    Other,
    TypeTool,
    Escape,
    CommitAndExit,
    NonTypeTool
}

public sealed record CanvasIntent(bool TypeToolArmed, bool IsEditing, string Reason);

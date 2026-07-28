namespace IllustratorTypeFlow;

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool DiagnosticsEnabled { get; set; }
    public Dictionary<string, FieldOverride> FieldOverrides { get; set; } = [];
}

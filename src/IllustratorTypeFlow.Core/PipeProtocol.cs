namespace IllustratorTypeFlow;

public static class PipeProtocol
{
    public const string PipeName = "IllustratorTypeFlow.v1";
    public const int Version = 1;

    public static PluginState ParseState(string? state) => state switch
    {
        "CanvasTextEditing" => PluginState.CanvasTextEditing,
        "NotEditing" => PluginState.NotEditing,
        _ => PluginState.Unavailable
    };
}

namespace IllustratorTypeFlow.Tests;

public sealed class PipeProtocolTests
{
    [Theory]
    [InlineData("CanvasTextEditing", PluginState.CanvasTextEditing)]
    [InlineData("NotEditing", PluginState.NotEditing)]
    [InlineData("Unavailable", PluginState.Unavailable)]
    [InlineData("unexpected", PluginState.Unavailable)]
    [InlineData(null, PluginState.Unavailable)]
    public void ParsesStates(string? value, PluginState expected)
    {
        Assert.Equal(expected, PipeProtocol.ParseState(value));
    }
}

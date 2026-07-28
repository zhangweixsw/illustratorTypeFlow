namespace IllustratorTypeFlow.Tests;

public sealed class StateReducerTests
{
    [Fact]
    public void DisabledRestoresInsteadOfManaging()
    {
        var decision = StateReducer.Decide(Input(
            enabled: false,
            illustrator: true,
            PluginState.CanvasTextEditing,
            FocusKind.CanvasText));

        Assert.False(decision.ManageIllustrator);
        Assert.False(decision.WantsChinese);
    }

    [Fact]
    public void LeavingIllustratorRestoresInsteadOfForcingEnglish()
    {
        var decision = StateReducer.Decide(Input(
            enabled: true,
            illustrator: false,
            PluginState.Unavailable,
            FocusKind.PluginTextField));

        Assert.False(decision.ManageIllustrator);
    }

    [Theory]
    [InlineData(FocusKind.CanvasText, true)]
    [InlineData(FocusKind.LayerOrArtboardName, true)]
    [InlineData(FocusKind.PluginTextField, true)]
    [InlineData(FocusKind.NumericParameter, false)]
    [InlineData(FocusKind.NonEditable, false)]
    public void AppliesExpectedImePolicy(FocusKind kind, bool wantsChinese)
    {
        var decision = StateReducer.Decide(Input(
            enabled: true,
            illustrator: true,
            PluginState.NotEditing,
            kind));

        Assert.True(decision.ManageIllustrator);
        Assert.Equal(wantsChinese, decision.WantsChinese);
    }

    private static CoordinatorInput Input(
        bool enabled,
        bool illustrator,
        PluginState pluginState,
        FocusKind kind) =>
        new(
            enabled,
            illustrator,
            pluginState,
            new ClassificationResult(kind, "test"));
}


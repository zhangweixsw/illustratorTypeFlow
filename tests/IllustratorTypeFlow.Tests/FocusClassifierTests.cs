namespace IllustratorTypeFlow.Tests;

public sealed class FocusClassifierTests
{
    [Fact]
    public void CanvasPluginStateWinsOverFocusedControl()
    {
        var classifier = new FocusClassifier();

        var result = classifier.Classify(Focus(ControlKind.Spinner, "宽度"), PluginState.CanvasTextEditing);

        Assert.Equal(FocusKind.CanvasText, result.Kind);
        Assert.True(result.WantsChinese);
    }

    [Theory]
    [InlineData("图层 1")]
    [InlineData("画板名称")]
    [InlineData("Rename Layer")]
    [InlineData("Artboard name")]
    public void RenameFieldsWantChinese(string name)
    {
        var classifier = new FocusClassifier();

        var result = classifier.Classify(Focus(ControlKind.Edit, name), PluginState.NotEditing);

        Assert.Equal(FocusKind.LayerOrArtboardName, result.Kind);
        Assert.True(result.WantsChinese);
    }

    [Theory]
    [InlineData(ControlKind.Spinner, "")]
    [InlineData(ControlKind.Edit, "宽度")]
    [InlineData(ControlKind.Edit, "Opacity")]
    [InlineData(ControlKind.Edit, "12.5 pt")]
    [InlineData(ControlKind.Edit, "75%")]
    public void NumericFieldsStayEnglish(ControlKind kind, string name)
    {
        var classifier = new FocusClassifier();

        var result = classifier.Classify(Focus(kind, name), PluginState.NotEditing);

        Assert.Equal(FocusKind.NumericParameter, result.Kind);
        Assert.False(result.WantsChinese);
    }

    [Fact]
    public void PlainPluginEditWantsChinese()
    {
        var classifier = new FocusClassifier();

        var result = classifier.Classify(
            Focus(ControlKind.Edit, "提示词", ancestors: ["我的插件[Chrome_WidgetWin_0]"]),
            PluginState.NotEditing);

        Assert.Equal(FocusKind.PluginTextField, result.Kind);
    }

    [Fact]
    public void DocumentSurfaceWithoutCaretStaysEnglish()
    {
        var classifier = new FocusClassifier();

        var result = classifier.Classify(
            Focus(ControlKind.Document, "Illustrator canvas"),
            PluginState.NotEditing);

        Assert.Equal(FocusKind.NonEditable, result.Kind);
        Assert.False(result.WantsChinese);
    }

    [Fact]
    public void UserCanExplicitlyMarkDocumentAsTextInput()
    {
        var focus = Focus(ControlKind.Document, "Legacy plug-in editor");
        var classifier = new FocusClassifier(new Dictionary<string, FieldOverride>
        {
            [focus.Signature] = FieldOverride.Text
        });

        var result = classifier.Classify(focus, PluginState.NotEditing);

        Assert.Equal(FocusKind.PluginTextField, result.Kind);
        Assert.True(result.WantsChinese);
    }

    [Fact]
    public void UserOverrideWinsOverNumericHeuristic()
    {
        var focus = Focus(ControlKind.Edit, "宽度备注");
        var overrides = new Dictionary<string, FieldOverride>
        {
            [focus.Signature] = FieldOverride.Text
        };
        var classifier = new FocusClassifier(overrides);

        var result = classifier.Classify(focus, PluginState.NotEditing);

        Assert.Equal(FocusKind.PluginTextField, result.Kind);
    }

    [Fact]
    public void OtherApplicationsAreNeverManaged()
    {
        var classifier = new FocusClassifier();
        var focus = Focus(ControlKind.Edit, "Message") with { IsIllustrator = false };

        var result = classifier.Classify(focus, PluginState.CanvasTextEditing);

        Assert.Equal(FocusKind.NonEditable, result.Kind);
    }

    private static FocusInfo Focus(
        ControlKind kind,
        string name,
        IReadOnlyList<string>? ancestors = null) =>
        new(
            IsIllustrator: true,
            ProcessId: 100,
            WindowHandle: 1,
            FocusHandle: 2,
            ControlKind: kind,
            Name: name,
            AutomationId: "",
            ClassName: "Edit",
            FrameworkId: "Win32",
            Ancestors: ancestors ?? []);
}

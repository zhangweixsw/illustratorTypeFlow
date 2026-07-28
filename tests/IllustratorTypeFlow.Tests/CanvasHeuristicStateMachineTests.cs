namespace IllustratorTypeFlow.Tests;

public sealed class CanvasHeuristicStateMachineTests
{
    [Fact]
    public void TypeToolThenCanvasClickEntersEditing()
    {
        var machine = new CanvasHeuristicStateMachine();

        machine.OnShortcut(CanvasShortcut.TypeTool, uiInputFocused: false);
        machine.OnCanvasClick(isDoubleClick: false);

        Assert.True(machine.State.TypeToolArmed);
        Assert.True(machine.State.IsEditing);
    }

    [Fact]
    public void TypeKeyInsideUiFieldDoesNotArmTool()
    {
        var machine = new CanvasHeuristicStateMachine();

        machine.OnShortcut(CanvasShortcut.TypeTool, uiInputFocused: true);
        machine.OnCanvasClick(isDoubleClick: false);

        Assert.False(machine.State.TypeToolArmed);
        Assert.False(machine.State.IsEditing);
    }

    [Fact]
    public void EscapeExitsButKeepsTypeToolArmed()
    {
        var machine = new CanvasHeuristicStateMachine();
        machine.OnShortcut(CanvasShortcut.TypeTool, uiInputFocused: false);
        machine.OnCanvasClick(isDoubleClick: false);

        machine.OnShortcut(CanvasShortcut.Escape, uiInputFocused: false);

        Assert.True(machine.State.TypeToolArmed);
        Assert.False(machine.State.IsEditing);
    }

    [Fact]
    public void DoubleClickCanEnterWithoutKnownTypeTool()
    {
        var machine = new CanvasHeuristicStateMachine();

        machine.OnCanvasClick(isDoubleClick: true);

        Assert.True(machine.State.IsEditing);
    }

    [Fact]
    public void ClickingUiTextInputLeavesCanvasEditing()
    {
        var machine = new CanvasHeuristicStateMachine();
        machine.SetManual(true);

        machine.OnNonCanvasClick(isTextInput: true);

        Assert.False(machine.State.IsEditing);
        Assert.True(machine.State.TypeToolArmed);
    }

    [Fact]
    public void LeavingIllustratorResetsEverything()
    {
        var machine = new CanvasHeuristicStateMachine();
        machine.SetManual(true);

        machine.Reset();

        Assert.False(machine.State.IsEditing);
        Assert.False(machine.State.TypeToolArmed);
    }
}

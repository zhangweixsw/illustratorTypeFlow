namespace IllustratorTypeFlow;

public sealed class StateCoordinator : IDisposable
{
    private readonly SynchronizationContext uiContext;
    private readonly FocusMonitor focusMonitor;
    private readonly PluginPipeServer pipeServer;
    private readonly CanvasHeuristicMonitor canvasHeuristic;
    private readonly ImeController imeController;
    private readonly FileLogger logger;
    private readonly System.Windows.Forms.Timer debounceTimer;
    private readonly System.Windows.Forms.Timer heartbeatTimer;
    private FocusClassifier classifier;

    public StateCoordinator(
        AppSettings settings,
        FocusMonitor focusMonitor,
        PluginPipeServer pipeServer,
        CanvasHeuristicMonitor canvasHeuristic,
        ImeController imeController,
        FileLogger logger)
    {
        Settings = settings;
        this.focusMonitor = focusMonitor;
        this.pipeServer = pipeServer;
        this.canvasHeuristic = canvasHeuristic;
        this.imeController = imeController;
        this.logger = logger;
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        classifier = new FocusClassifier(settings.FieldOverrides);

        debounceTimer = new System.Windows.Forms.Timer { Interval = 15 };
        debounceTimer.Tick += (_, _) =>
        {
            debounceTimer.Stop();
            Evaluate();
        };

        heartbeatTimer = new System.Windows.Forms.Timer { Interval = 120 };
        heartbeatTimer.Tick += (_, _) => RequestEvaluation();

        focusMonitor.FocusChanged += OnSignal;
        pipeServer.StateChanged += OnPluginStateChanged;
        canvasHeuristic.StateChanged += OnCanvasSignal;
        heartbeatTimer.Start();
        RequestEvaluation();
    }

    public AppSettings Settings { get; }
    public FocusInfo? CurrentFocus { get; private set; }
    public CoordinatorDecision CurrentDecision { get; private set; } =
        new(false, false, FocusKind.NonEditable, "初始化");

    public event EventHandler? StateChanged;

    public void RequestEvaluation()
    {
        if (SynchronizationContext.Current == uiContext)
        {
            debounceTimer.Stop();
            debounceTimer.Interval = 15;
            debounceTimer.Start();
        }
        else
        {
            uiContext.Post(_ => RequestEvaluation(), null);
        }
    }

    public void SetEnabled(bool enabled)
    {
        Settings.Enabled = enabled;
        if (!enabled)
            imeController.Restore();
        RequestEvaluation();
    }

    public void SetOverride(FieldOverride fieldOverride)
    {
        if (CurrentFocus is null || !CurrentFocus.IsIllustrator)
            return;

        if (fieldOverride == FieldOverride.None)
            Settings.FieldOverrides.Remove(CurrentFocus.Signature);
        else
            Settings.FieldOverrides[CurrentFocus.Signature] = fieldOverride;

        classifier = new FocusClassifier(Settings.FieldOverrides);
        RequestEvaluation();
    }

    public void SetManualCanvasEditing(bool editing)
    {
        canvasHeuristic.SetManualEditing(editing);
        RequestEvaluation();
    }

    private void Evaluate()
    {
        try
        {
            var focus = focusMonitor.Capture();
            canvasHeuristic.SetUiInputFocused(
                focus.ControlKind is ControlKind.Edit or ControlKind.Spinner);
            if (!focus.IsIllustrator)
                canvasHeuristic.ResetIfOutsideIllustrator();

            ApplyDecision(focus);
        }
        catch (Exception exception)
        {
            logger.Error("状态评估失败", exception);
        }
    }

    private void ApplyDecision(FocusInfo focus)
    {
        var effectiveCanvasState = pipeServer.State != PluginState.Unavailable
            ? pipeServer.State
            : canvasHeuristic.State.IsEditing
                ? PluginState.CanvasTextEditing
                : PluginState.NotEditing;
        var classification = classifier.Classify(focus, effectiveCanvasState);
        var decision = StateReducer.Decide(new CoordinatorInput(
            Settings.Enabled, focus.IsIllustrator, effectiveCanvasState, classification));

        CurrentFocus = focus;
        var changed = decision != CurrentDecision;
        CurrentDecision = decision;

        if (!decision.ManageIllustrator)
            imeController.Restore();
        else if (!imeController.Apply(focus, decision.WantsChinese))
            ScheduleCompositionRetry();

        if (changed)
        {
            logger.Info($"识别状态：{decision.EffectiveKind}；{decision.Reason}");
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ScheduleCompositionRetry()
    {
        debounceTimer.Stop();
        debounceTimer.Interval = 25;
        debounceTimer.Start();
    }

    private void OnSignal(object? sender, EventArgs args) => RequestEvaluation();

    private void OnCanvasSignal(object? sender, EventArgs args)
    {
        // The mouse/keyboard hook already established the canvas transition.
        // Reusing the last Illustrator focus avoids a comparatively expensive
        // UIA capture, so IME closes or opens before the next user keystroke.
        uiContext.Post(_ =>
        {
            if (CurrentFocus is { IsIllustrator: true } focus)
                ApplyDecision(focus);
            else
                RequestEvaluation();
        }, null);
    }

    private void OnPluginStateChanged(object? sender, PluginState state) => RequestEvaluation();

    public void Dispose()
    {
        heartbeatTimer.Stop();
        debounceTimer.Stop();
        focusMonitor.FocusChanged -= OnSignal;
        pipeServer.StateChanged -= OnPluginStateChanged;
        canvasHeuristic.StateChanged -= OnCanvasSignal;
        imeController.Restore();
        heartbeatTimer.Dispose();
        debounceTimer.Dispose();
    }
}

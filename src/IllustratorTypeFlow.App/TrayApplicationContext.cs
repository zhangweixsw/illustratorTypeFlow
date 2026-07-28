using System.Diagnostics;
using System.Text;

namespace IllustratorTypeFlow;

public sealed class TrayApplicationContext : ApplicationContext, IDisposable
{
    private readonly string dataDirectory;
    private readonly FileLogger logger;
    private readonly SettingsStore settingsStore;
    private readonly AppSettings settings;
    private readonly FocusMonitor focusMonitor;
    private readonly PluginPipeServer pipeServer;
    private readonly CanvasHeuristicMonitor canvasHeuristic;
    private readonly ImeController imeController;
    private readonly StateCoordinator coordinator;
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem enabledItem;
    private readonly ToolStripMenuItem startupItem;
    private readonly ToolStripMenuItem statusItem;
    private readonly Control dispatcher = new();
    private readonly Icon appIcon;
    private bool disposed;

    public TrayApplicationContext()
    {
        dispatcher.CreateControl();
        dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IllustratorTypeFlow");
        logger = new FileLogger(Path.Combine(dataDirectory, "logs"));
        settingsStore = new SettingsStore(dataDirectory, logger);
        settings = settingsStore.Load();

        focusMonitor = new FocusMonitor(logger);
        pipeServer = new PluginPipeServer(logger);
        canvasHeuristic = new CanvasHeuristicMonitor(logger);
        imeController = new ImeController(logger);
        coordinator = new StateCoordinator(
            settings, focusMonitor, pipeServer, canvasHeuristic, imeController, logger);
        coordinator.StateChanged += OnStateChanged;

        statusItem = new ToolStripMenuItem("状态：初始化") { Enabled = false };
        enabledItem = new ToolStripMenuItem("启用智能切换")
        {
            Checked = settings.Enabled,
            CheckOnClick = true
        };
        enabledItem.CheckedChanged += (_, _) =>
        {
            coordinator.SetEnabled(enabledItem.Checked);
            settingsStore.Save(settings);
            UpdateTray();
        };

        startupItem = new ToolStripMenuItem("开机启动")
        {
            Checked = StartupManager.IsEnabled(),
            CheckOnClick = true
        };
        startupItem.CheckedChanged += (_, _) =>
        {
            try
            {
                StartupManager.SetEnabled(startupItem.Checked);
                settings.StartWithWindows = startupItem.Checked;
                settingsStore.Save(settings);
            }
            catch (Exception exception)
            {
                logger.Error("设置开机启动失败", exception);
                startupItem.Checked = StartupManager.IsEnabled();
            }
        };

        var markTextItem = new ToolStripMenuItem("将当前输入框设为文字框");
        markTextItem.Click += (_, _) => SaveOverride(FieldOverride.Text);
        var markNumericItem = new ToolStripMenuItem("将当前输入框设为参数框");
        markNumericItem.Click += (_, _) => SaveOverride(FieldOverride.Numeric);
        var clearRuleItem = new ToolStripMenuItem("清除当前输入框规则");
        clearRuleItem.Click += (_, _) => SaveOverride(FieldOverride.None);
        var canvasTextItem = new ToolStripMenuItem("无插件校正：正在编辑画布文字")
        {
            CheckOnClick = true
        };
        canvasTextItem.CheckedChanged += (_, _) =>
        {
            coordinator.SetManualCanvasEditing(canvasTextItem.Checked);
            UpdateTray();
        };

        var copyDiagnosticsItem = new ToolStripMenuItem("复制当前诊断信息");
        copyDiagnosticsItem.Click += (_, _) => CopyDiagnostics();
        var openLogsItem = new ToolStripMenuItem("打开日志目录");
        openLogsItem.Click += (_, _) => Process.Start(new ProcessStartInfo
        {
            FileName = logger.DirectoryPath,
            UseShellExecute = true
        });
        var helpItem = new ToolStripMenuItem("使用帮助");
        helpItem.Click += (_, _) => OpenWebsite("https://mootop.top/docs/illustrator-typeflow/");

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitThread();
        var signatureItem = new ToolStripMenuItem("mootop.top");
        signatureItem.Click += (_, _) => OpenWebsite("https://mootop.top/");

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            statusItem,
            new ToolStripSeparator(),
            enabledItem,
            startupItem,
            new ToolStripSeparator(),
            markTextItem,
            markNumericItem,
            clearRuleItem,
            canvasTextItem,
            new ToolStripSeparator(),
            copyDiagnosticsItem,
            openLogsItem,
            helpItem,
            new ToolStripSeparator(),
            exitItem,
            new ToolStripSeparator(),
            signatureItem
        ]);

        menu.Opening += (_, _) =>
        {
            var canClassify = coordinator.CurrentFocus?.IsIllustrator == true;
            markTextItem.Enabled = canClassify;
            markNumericItem.Enabled = canClassify;
            clearRuleItem.Enabled = canClassify;
            canvasTextItem.Enabled = canClassify && pipeServer.State == PluginState.Unavailable;
            canvasTextItem.Checked = canvasHeuristic.State.IsEditing;
        };

        using var extractedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        appIcon = extractedIcon is null
            ? (Icon)SystemIcons.Application.Clone()
            : (Icon)extractedIcon.Clone();
        trayIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = "Illustrator 智能输入法",
            ContextMenuStrip = menu,
            Visible = true
        };
        trayIcon.DoubleClick += (_, _) =>
            MessageBox.Show(
                BuildDiagnostics(),
                "Illustrator 智能输入法",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

        if (settings.StartWithWindows != StartupManager.IsEnabled())
        {
            StartupManager.SetEnabled(settings.StartWithWindows);
            startupItem.Checked = settings.StartWithWindows;
        }

        UpdateTray();
    }

    private void OnStateChanged(object? sender, EventArgs args)
    {
        if (trayIcon.ContextMenuStrip?.InvokeRequired == true)
            trayIcon.ContextMenuStrip.BeginInvoke(UpdateTray);
        else
            UpdateTray();
    }

    private void UpdateTray()
    {
        var decision = coordinator.CurrentDecision;
        statusItem.Text = $"状态：{ToChinese(decision.EffectiveKind)}";
        trayIcon.Text = $"Illustrator 输入法：{ToChinese(decision.EffectiveKind)}";
    }

    private void SaveOverride(FieldOverride fieldOverride)
    {
        coordinator.SetOverride(fieldOverride);
        settingsStore.Save(settings);
    }

    private void CopyDiagnostics()
    {
        try
        {
            Clipboard.SetText(BuildDiagnostics());
            trayIcon.ShowBalloonTip(1500, "Illustrator 智能输入法", "诊断信息已复制", ToolTipIcon.Info);
        }
        catch (Exception exception)
        {
            logger.Error("复制诊断信息失败", exception);
        }
    }

    private void OpenWebsite(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            logger.Error($"打开网页失败：{url}", exception);
        }
    }

    private string BuildDiagnostics()
    {
        var focus = coordinator.CurrentFocus;
        var builder = new StringBuilder()
            .AppendLine($"Enabled: {settings.Enabled}")
            .AppendLine($"PluginState: {pipeServer.State}")
            .AppendLine($"FocusKind: {coordinator.CurrentDecision.EffectiveKind}")
            .AppendLine($"Reason: {coordinator.CurrentDecision.Reason}");
        if (focus is not null)
        {
            builder
                .AppendLine($"Illustrator: {focus.IsIllustrator}")
                .AppendLine($"ProcessId: {focus.ProcessId}")
                .AppendLine($"ControlKind: {focus.ControlKind}")
                .AppendLine($"Name: {focus.Name}")
                .AppendLine($"AutomationId: {focus.AutomationId}")
                .AppendLine($"ClassName: {focus.ClassName}")
                .AppendLine($"FrameworkId: {focus.FrameworkId}")
                .AppendLine($"Ancestors: {string.Join(" > ", focus.Ancestors)}")
                .AppendLine($"Signature: {focus.Signature}");
        }

        builder.AppendLine().AppendLine("mootop.top");
        return builder.ToString();
    }

    private static string ToChinese(FocusKind kind) => kind switch
    {
        FocusKind.CanvasText => "画布文字（中文）",
        FocusKind.LayerOrArtboardName => "图层/画板命名（中文）",
        FocusKind.PluginTextField => "文字输入框（中文）",
        FocusKind.NumericParameter => "参数输入（英文）",
        _ => "快捷键（英文）"
    };

    public void RequestExit()
    {
        if (dispatcher.InvokeRequired)
            dispatcher.BeginInvoke(new Action(ExitThread));
        else
            ExitThread();
    }

    protected override void ExitThreadCore()
    {
        Dispose();
        base.ExitThreadCore();
    }

    public new void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        trayIcon.Visible = false;
        settingsStore.Save(settings);
        coordinator.StateChanged -= OnStateChanged;
        coordinator.Dispose();
        pipeServer.Dispose();
        canvasHeuristic.Dispose();
        focusMonitor.Dispose();
        trayIcon.Dispose();
        appIcon.Dispose();
        dispatcher.Dispose();
        logger.Dispose();
    }
}

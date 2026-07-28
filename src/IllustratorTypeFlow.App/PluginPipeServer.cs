using System.IO.Pipes;
using System.Text.Json;

namespace IllustratorTypeFlow;

public sealed class PluginPipeServer : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly FileLogger logger;
    private readonly Task listener;
    private readonly Task watchdog;
    private long lastMessageTicks;

    public PluginPipeServer(FileLogger logger)
    {
        this.logger = logger;
        listener = Task.Run(ListenAsync);
        watchdog = Task.Run(WatchdogAsync);
    }

    public event EventHandler<PluginState>? StateChanged;

    public PluginState State { get; private set; } = PluginState.Unavailable;

    private async Task ListenAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    PipeProtocol.PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await pipe.WaitForConnectionAsync(cancellation.Token).ConfigureAwait(false);
                logger.Info("Illustrator 插件已连接");
                using var reader = new StreamReader(pipe);
                while (pipe.IsConnected && !cancellation.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellation.Token).ConfigureAwait(false);
                    if (line is null)
                        break;

                    HandleMessage(line);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.Error("插件管道异常", exception);
                await Task.Delay(500, cancellation.Token).ConfigureAwait(false);
            }
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize<PluginMessage>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (message is null || message.Protocol != PipeProtocol.Version)
                return;

            Interlocked.Exchange(ref lastMessageTicks, DateTime.UtcNow.Ticks);
            SetState(PipeProtocol.ParseState(message.State));
        }
        catch (JsonException exception)
        {
            logger.Error("忽略无效插件消息", exception);
        }
    }

    private async Task WatchdogAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(250, cancellation.Token).ConfigureAwait(false);
                var ticks = Interlocked.Read(ref lastMessageTicks);
                if (ticks != 0 &&
                    new TimeSpan(DateTime.UtcNow.Ticks - ticks) > TimeSpan.FromMilliseconds(1500))
                {
                    Interlocked.Exchange(ref lastMessageTicks, 0);
                    SetState(PluginState.Unavailable);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private void SetState(PluginState state)
    {
        if (State == state)
            return;

        State = state;
        StateChanged?.Invoke(this, state);
        logger.Info($"插件状态：{state}");
    }

    public void Dispose()
    {
        cancellation.Cancel();
        try
        {
            listener.Wait(TimeSpan.FromSeconds(2));
            watchdog.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Shutdown must not block the tray process.
        }
        cancellation.Dispose();
    }
}

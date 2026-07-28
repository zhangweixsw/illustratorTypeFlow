using System.Threading;

namespace IllustratorTypeFlow;

internal static class Program
{
    private const string MutexName = @"Local\IllustratorTypeFlow.Singleton";

    [STAThread]
    private static void Main(string[] args)
    {
        using var singleton = new Mutex(true, MutexName, out var isFirst);
        if (!isFirst)
            return;

        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext();
        if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(4000).ConfigureAwait(false);
                context.RequestExit();
            });
        }
        Application.Run(context);
    }
}

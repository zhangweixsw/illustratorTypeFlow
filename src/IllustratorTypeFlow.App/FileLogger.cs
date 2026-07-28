using System.Text;
using System.IO;

namespace IllustratorTypeFlow;

public sealed class FileLogger : IDisposable
{
    private readonly object sync = new();
    private readonly StreamWriter writer;

    public FileLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        var path = Path.Combine(logDirectory, $"typeflow-{DateTime.Now:yyyyMMdd}.log");
        writer = new StreamWriter(path, append: true, new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        Log("INFO", "IllustratorTypeFlow 启动");
    }

    public string DirectoryPath => Path.GetDirectoryName(writer.BaseStream is FileStream fs ? fs.Name : "") ?? "";

    public void Info(string message) => Log("INFO", message);
    public void Warn(string message) => Log("WARN", message);
    public void Error(string message, Exception exception) =>
        Log("ERROR", $"{message}: {exception.GetType().Name}: {exception.Message}");

    private void Log(string level, string message)
    {
        lock (sync)
            writer.WriteLine($"{DateTimeOffset.Now:O}\t{level}\t{message.ReplaceLineEndings(" ")}");
    }

    public void Dispose()
    {
        Log("INFO", "IllustratorTypeFlow 退出");
        writer.Dispose();
    }
}

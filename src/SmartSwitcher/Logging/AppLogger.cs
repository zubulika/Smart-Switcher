namespace SmartSwitcher.Logging;

public class AppLogger
{
    private static readonly object _lock = new();
    private static string? _logFilePath;

    public static event Action<string>? OnLogEntry;

    public static string LogFilePath
    {
        get
        {
            if (_logFilePath == null)
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SmartSwitcher",
                    "logs");
                Directory.CreateDirectory(dir);
                _logFilePath = Path.Combine(dir, "smart_switcher.log");
            }
            return _logFilePath;
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message) => Log("ERROR", message);

    public static void LogDecision(string ethStats, string wifiStats, string decision, string? routeChange = null)
    {
        string time = DateTime.Now.ToString("HH:mm:ss");
        LogRaw($"[{time}] Ethernet: {ethStats}");
        LogRaw($"[{time}] Wi-Fi:    {wifiStats}");
        LogRaw($"[{time}] Decision: {decision}");
        if (!string.IsNullOrEmpty(routeChange))
        {
            LogRaw($"[{time}] Route preference changed: {routeChange}");
        }
    }

    public static void LogRaw(string formattedLine)
    {
        lock (_lock)
        {
            try
            {
                File.AppendAllText(LogFilePath, formattedLine + Environment.NewLine);
            }
            catch
            {
                // Best effort logging
            }
        }

        Console.WriteLine(formattedLine);
        OnLogEntry?.Invoke(formattedLine);
    }

    private static void Log(string level, string message)
    {
        string time = DateTime.Now.ToString("HH:mm:ss");
        string line = $"[{time}] [{level}] {message}";
        LogRaw(line);
    }
}

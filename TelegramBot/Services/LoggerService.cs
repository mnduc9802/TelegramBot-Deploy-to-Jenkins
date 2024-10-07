using Serilog;
using Serilog.Events;

namespace TelegramBot.Services
{
    public static class LoggerService
    {
        private static ILogger _logger;
        public static string? LogFolderPath { get; private set; }
        public static string? LogFilePath { get; private set; }
        private static string? _currentLogDate; 
        private static Timer? _timeCheckTimer;
        private static DateTime _lastCheckedTime;

        public static void Initialize()
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                LogFolderPath = Path.Combine(baseDirectory, "logs");
                Directory.CreateDirectory(LogFolderPath);

                UpdateLogger();

                Log.Logger = _logger;

                _lastCheckedTime = DateTime.Now;
                _timeCheckTimer = new Timer(CheckTimeChange, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

                LogInformation($"Log initialization completed. File: {LogFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
                Console.WriteLine($"Exception details: {ex}");
                throw;
            }
        }

        private static void CheckTimeChange(object state)
        {
            DateTime currentTime = DateTime.Now;
            if (currentTime.Date != _lastCheckedTime.Date ||
                Math.Abs((currentTime - _lastCheckedTime).TotalMinutes) > 1)
            {
                UpdateLogger();
            }
            _lastCheckedTime = currentTime;
        }

        private static void UpdateLogger()
        {
            string currentDate = DateTime.Now.ToString("dd-MM-yyyy");

            if (_currentLogDate != currentDate)
            {
                _currentLogDate = currentDate;
                string fileName = $"telebot-deploy-log-{_currentLogDate}.txt";
                LogFilePath = Path.Combine(LogFolderPath, fileName);

                _logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithThreadId()
                    .WriteTo.Console(outputTemplate:
                        "[{Timestamp:dd-MM-yyyy HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        path: LogFilePath,
                        fileSizeLimitBytes: null,
                        shared: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(1),
                        outputTemplate: "{Timestamp:dd-MM-yyyy HH:mm:ss.fff} [{Level:u3}] [Thread:{ThreadId}]{NewLine}" +
                                       "Message: {Message:lj}{NewLine}" +
                                       "{Exception}{NewLine}")
                    .CreateLogger();

                CleanupOldLogs();
                LogInformation($"Logger updated. New log file: {LogFilePath}");
            }
        }

        private static void CleanupOldLogs()
        {
            try
            {
                var directory = new DirectoryInfo(LogFolderPath);
                var logFiles = directory.GetFiles("telebot-deploy-log-*.txt")
                                        .OrderBy(f => f.CreationTime)  // Sort by creation time to get the oldest first
                                        .ToList();

                // Log the number of files found
                LogInformation($"Found {logFiles.Count} log files.");

                // Keep only the most recent 31 files, delete the rest
                if (logFiles.Count > 31)
                {
                    int filesToDelete = logFiles.Count - 31;

                    foreach (var file in logFiles.Take(filesToDelete))  // Delete only the excess files
                    {
                        try
                        {
                            LogInformation($"Attempting to delete log file: {file.Name}, created on {file.CreationTime}");

                            file.Delete();  // Try to delete the file
                            LogInformation($"Deleted old log file: {file.Name}");
                        }
                        catch (Exception ex)
                        {
                            LogError(ex, $"Failed to delete old log file {file.Name}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    LogInformation("No files to delete, count is less than or equal to 31.");
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "Error during log cleanup.");
            }
        }

        public static void LogInformation(string message, params object[] propertyValues)
        {
            try
            {
                _logger?.Information(message, propertyValues);
            }
            catch
            {
                Console.WriteLine($"INFO [{DateTime.Now:dd-MM-yyyy HH:mm:ss.fff}]: {message}");
            }
        }

        public static void LogDebug(string message, params object[] propertyValues)
        {
            try
            {
                _logger?.Debug(message, propertyValues);
            }
            catch
            {
                Console.WriteLine($"DEBUG [{DateTime.Now:dd-MM-yyyy HH:mm:ss.fff}]: {message}");
            }
        }

        public static void LogWarning(string message, params object[] propertyValues)
        {
            try
            {
                _logger?.Warning(message, propertyValues);
            }
            catch
            {
                Console.WriteLine($"WARNING [{DateTime.Now:dd-MM-yyyy HH:mm:ss.fff}]: {message}");
            }
        }
        public static void LogError(Exception ex, string message, params object[] propertyValues)
        {
            try
            {
                _logger?.Error(ex, message, propertyValues);
            }
            catch
            {
                Console.WriteLine($"ERROR [{DateTime.Now:dd-MM-yyyy HH:mm:ss.fff}]: {message} - {ex}");
            }
        }
    }
}
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public static class GlobalLogger
{
    private static readonly object _lockObj = new object();
    private static readonly string _baseLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);

    /// <summary>
    /// Format datetime to "yyyy-MM-dd HH:mm:ss" in IST timezone
    /// </summary>
    private static string FormatTime(DateTime dateTime)
    {
        // Convert UTC to IST
        TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        DateTime istTime = TimeZoneInfo.ConvertTimeFromUtc(dateTime.ToUniversalTime(), istZone);

        return istTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Append a log message to a daily log file
    /// </summary>
    /// <param name="logMessage">Message to log</param>
    /// <param name="logSubfolder">Optional subfolder under Logs (default: App_Logs)</param>
    public static async Task LogToFileAsync( string logMessage, string logSubfolder = "App_Logs")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logMessage))
                return;

            string logsFolder = Path.Combine(_baseLogDir, logSubfolder);

            // Ensure directory exists
            if (!Directory.Exists(logsFolder))
            {
                Directory.CreateDirectory(logsFolder);
            }

            DateTime now = DateTime.UtcNow;
            string datePart = now.ToString("yyyy-MM-dd");
            string logFileName = $"{datePart}.log";
            string logFilePath = Path.Combine(logsFolder, logFileName);

            string timestamp = FormatTime(now);
            string logLine = $"[{timestamp}] {logMessage}{Environment.NewLine}";

            //// Thread-safe write
            //lock (_lockObj)
            //{
            //    File.AppendAllText(logFilePath, logLine, Encoding.UTF8);
            //}

            // If you want async version:
            await File.AppendAllTextAsync(logFilePath, logLine, Encoding.UTF8);
        }
        catch (Exception ex)
        {
           
            string logsFolder = Path.Combine(_baseLogDir, logSubfolder);

            // Ensure directory exists
            if (!Directory.Exists(logsFolder))
            {
                Directory.CreateDirectory(logsFolder);
            }

            DateTime now = DateTime.UtcNow;
            string datePart = now.ToString("yyyy-MM-dd");
            string logFileName = $"{datePart}_Error.log";
            string logFilePath = Path.Combine(logsFolder, logFileName);

            string timestamp = FormatTime(now);
            string logLine = $"[{timestamp}] {ex}{Environment.NewLine}";
           
        }
        
    }
}

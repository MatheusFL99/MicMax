using System;
using System.Diagnostics;

namespace MicMax.Core.Logging;

/// <summary>
/// Structured logging and Windows Event Log writer.
/// Provides logging for lock events, correction actions, and application lifecycle.
/// </summary>
public static class EventLogger
{
    /// <summary>
    /// Writes an informational message to the Event Log and/or debug output.
    /// </summary>
    /// <param name="message">The log message.</param>
    /// <param name="eventType">The type of event (for Event Log only).</param>
    public static void LogInfo(string message, EventLogEntryType eventType = EventLogEntryType.Information)
    {
        WriteToEventLog(message, eventType);
        Debug.WriteLine($"[INFO] {message}");
    }

    /// <summary>
    /// Writes a warning message to the Event Log and/or debug output.
    /// </summary>
    /// <param name="message">The log message.</param>
    public static void LogWarning(string message)
    {
        WriteToEventLog(message, EventLogEntryType.Warning);
        Debug.WriteLine($"[WARNING] {message}");
    }

    /// <summary>
    /// Writes an error message to the Event Log and/or debug output.
    /// </summary>
    /// <param name="message">The log message.</param>
    public static void LogError(string message)
    {
        WriteToEventLog(message, EventLogEntryType.Error);
        Debug.WriteLine($"[ERROR] {message}");
    }

    /// <summary>
    /// Writes a verbose message to debug output only.
    /// </summary>
    /// <param name="message">The log message.</param>
    public static void LogVerbose(string message)
    {
        Debug.WriteLine($"[VERBOSE] {message}");
    }

    private static void WriteToEventLog(string message, EventLogEntryType eventType)
    {
        try
        {
            using (var eventLog = new EventLog("Application"))
            {
                if (!EventLog.SourceExists("MicMax"))
                {
                    EventLog.CreateEventSource("MicMax", "Application");
                }

                eventLog.Source = "MicMax";
                eventLog.WriteEntry(message, eventType);
            }
        }
        catch
        {
            // If Event Log is not available (e.g., not running on Windows with Event Log),
            // fall back to debug output only.
            Debug.WriteLine($"[EVENTLOG] {message}");
        }
    }
}
using System;
using System.IO;
using System.Text.Json;
using CspAnalyzer.Desktop.Models;

namespace CspAnalyzer.Desktop.Services;

/// <summary>
/// Persists AppSettings as JSON under the OS application-data folder
/// (S11b), following S11's SpecialFolder-based cross-platform idiom
/// (BackendEnvironment.PythonExecutable). Never throws: a missing file,
/// corrupt JSON, or any read/write failure falls back to/silently drops
/// the change rather than crashing the app or a settings-related dialog.
/// No logging framework exists in this codebase (checked - none used
/// anywhere), so failures are swallowed rather than logged.
/// </summary>
public class SettingsService
{
    private readonly string _filePath;

    public SettingsService(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath();
    }

    private static string DefaultFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "CspAnalyzer", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort persistence - losing a settings write on exit isn't fatal.
        }
    }
}

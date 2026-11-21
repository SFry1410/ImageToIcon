namespace ImageToIcon.Library;

/// Minimal logging hook. The host (CLI or GUI) decides where messages go.
public interface IIconLibraryLog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

public sealed class NullLog : IIconLibraryLog
{
    public static readonly NullLog Instance = new();
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
}

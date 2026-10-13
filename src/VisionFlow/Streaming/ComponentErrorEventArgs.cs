namespace VisionFlow.Streaming;

/// <summary>
/// Describes an error that occurred inside a component.
/// </summary>
public sealed class ComponentErrorEventArgs : EventArgs
{
    public ComponentErrorEventArgs(Exception exception, string context)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Context = context ?? string.Empty;
    }

    /// <summary>The original exception.</summary>
    public Exception Exception { get; }

    /// <summary>
    /// Where the error happened (e.g. "ProcessFrame", "DispatchLoop").
    /// </summary>
    public string Context { get; }

    public override string ToString() => $"[{Context}] {Exception.Message}";
}
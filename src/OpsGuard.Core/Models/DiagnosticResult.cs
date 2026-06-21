namespace OpsGuard.Core.Models;

public sealed class DiagnosticResult<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public string? Error { get; init; }

    public static DiagnosticResult<T> Ok(T data) => new() { Success = true, Data = data };

    public static DiagnosticResult<T> Fail(string error) => new() { Success = false, Error = error };
}

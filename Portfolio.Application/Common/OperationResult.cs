namespace Portfolio.Application.Common;

public enum FailureKind { Validation, Conflict }

public sealed record OperationResult<T>
{
    private OperationResult(T? value, string? error, FailureKind? failure)
        => (Value, Error, Failure) = (value, error, failure);

    public T? Value { get; }
    public string? Error { get; }
    public FailureKind? Failure { get; }
    public bool IsSuccess => Failure is null;

    public static OperationResult<T> Success(T value) => new(value, null, null);
    public static OperationResult<T> Invalid(string error) => new(default, error, FailureKind.Validation);
    public static OperationResult<T> Conflict(string error) => new(default, error, FailureKind.Conflict);
}

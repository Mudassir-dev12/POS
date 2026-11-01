namespace ShopPOS.Core.Models;

/// <summary>
/// Represents the outcome of a domain service operation.
/// </summary>
public class Result
{
    public bool Success { get; protected set; }
    public string? ErrorMessage { get; protected set; }
    public string? Error => ErrorMessage;
    public IDictionary<string, string[]> ValidationErrors { get; protected set; } = new Dictionary<string, string[]>();

    public bool IsFailure => !Success;

    protected Result(bool success, string? errorMessage, IDictionary<string, string[]>? validationErrors = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
        if (validationErrors != null)
        {
            ValidationErrors = validationErrors;
        }
    }

    public static Result Ok() => new(true, null);

    public static Result Failure(string errorMessage) => new(false, errorMessage);

    public static Result Invalid(IDictionary<string, string[]> validationErrors, string? errorMessage = "One or more validation errors occurred.")
        => new(false, errorMessage, validationErrors);

    public static Result<T> Ok<T>(T data) => Result<T>.Ok(data);

    public static Result<T> Failure<T>(string errorMessage) => Result<T>.Failure(errorMessage);

    public static Result<T> Invalid<T>(IDictionary<string, string[]> validationErrors, string? errorMessage = "One or more validation errors occurred.")
        => Result<T>.Invalid(validationErrors, errorMessage);
}

/// <summary>
/// Represents the outcome of a domain service operation returning data of type T.
/// </summary>
/// <typeparam name="T">Payload data type.</typeparam>
public class Result<T> : Result
{
    public T? Data { get; private set; }

    protected Result(bool success, T? data, string? errorMessage, IDictionary<string, string[]>? validationErrors = null)
        : base(success, errorMessage, validationErrors)
    {
        Data = data;
    }

    public static Result<T> Ok(T data) => new(true, data, null);

    public new static Result<T> Failure(string errorMessage) => new(false, default, errorMessage);

    public new static Result<T> Invalid(IDictionary<string, string[]> validationErrors, string? errorMessage = "One or more validation errors occurred.")
        => new(false, default, errorMessage, validationErrors);
}


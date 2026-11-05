namespace TheSpanishWay.Domain.Common;

/// <summary>
/// The outcome of a domain operation. A broken rule is an expected outcome and is
/// returned, not thrown: exceptions are reserved for programmer error and infrastructure
/// failure. See application/mediator, "Errors are values".
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        // A success carrying an error, or a failure carrying none, is always a construction bug.
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>Fails with the first failure in <paramref name="results"/>, else succeeds.</summary>
    public static Result FirstFailureOr(params Result[] results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                return Failure(result.Error);
            }
        }

        return Success();
    }
}

/// <inheritdoc cref="Result"/>
/// <typeparam name="TValue">The value produced on success.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    /// <summary>
    /// The produced value. Reading it on a failure throws, because asking for a value
    /// that was never produced is a programmer error, not a runtime condition to handle.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot read the value of a failed result. Error was: {Error}");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}

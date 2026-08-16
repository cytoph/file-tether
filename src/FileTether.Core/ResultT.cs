using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace FileTether.Core;

public sealed class Result<T> : Result
{
    [MemberNotNullWhen(true, nameof(Value))]
    public override bool Success => base.Success;

    public T? Value { get; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public Result(T value) : base()
    {
        Value = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public Result(string message) : base(message)
    {
        Value = default;
    }

    public static implicit operator Result<T>(T value) => new(value);
}

public partial class Result
{
    public static Result<T> Ok<T>(T value) => new(value);

    public static Result<T> Fail<T>(string message) => new(message);
}

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace FileTether.Core;

public partial class Result
{
    [MemberNotNullWhen(false, nameof(Message))]
    public virtual bool Success { get; }

    public string? Message { get; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public Result()
    {
        Success = true;
        Message = null;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public Result(string message)
    {
        Success = false;
        Message = message;
    }

    public static Result Ok() => new();

    public static Result Fail(string message) => new(message);
}

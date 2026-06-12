namespace AccountingSaaS.Shared.Responses;

public sealed class BaseResponseDto<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public List<string> Errors { get; init; } = [];

    public static BaseResponseDto<T> Ok(T? data, string message = "Success.") => new()
    {
        Success = true,
        Message = message,
        Data = data
    };

    public static BaseResponseDto<T> Fail(string message, IEnumerable<string>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors?.ToList() ?? []
    };
}

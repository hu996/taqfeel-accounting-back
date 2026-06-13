namespace AccountingSaaS.Shared.Responses;

public sealed class BaseResponseDto<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public List<string> Errors { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public int HttpStatusCode { get; init; } = 200;

    public static BaseResponseDto<T> Ok(T? data, string message = "تمت العملية بنجاح.") => new()
    {
        Success = true,
        Message = message,
        Data = data
    };

    public static BaseResponseDto<T> Fail(string message, IEnumerable<string>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors?.ToList() ?? [],
        HttpStatusCode = 400
    };

    public static BaseResponseDto<T> NotFound(string message, IEnumerable<string>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Errors = errors?.ToList() ?? [],
        HttpStatusCode = 404
    };
}

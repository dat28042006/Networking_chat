namespace ChatApp.Server;

/// <summary>A failure that is the client's fault. The Dispatcher turns it into {"ok":false,"error":{code,message}}.</summary>
public sealed class ApiException : Exception
{
    public string Code { get; }

    public ApiException(string code, string message) : base(message) => Code = code;

    public static ApiException BadRequest(string message) => new("BAD_REQUEST", message);
}

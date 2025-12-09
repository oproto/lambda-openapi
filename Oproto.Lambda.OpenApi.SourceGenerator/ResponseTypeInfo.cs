namespace Oproto.Lambda.OpenApi.SourceGenerator;

public class ResponseTypeInfo
{
    public Type ResponseType { get; set; }
    public int StatusCode { get; set; }
    public string Description { get; set; }
    public string ContentType { get; set; }
}

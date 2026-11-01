
namespace WhatsAppGateway.Features;

public sealed class WhatsAppOptions
{
    public string GraphApiVersion { get; set; } = "v21.0";
    public string PhoneNumberId   { get; set; } = "";
    public string AccessToken     { get; set; } = "";
    public string VerifyToken     { get; set; } = "";
    public string AppSecret       { get; set; } = "";
    public string ApiKey          { get; set; } = ""; // لحماية /api/send
}

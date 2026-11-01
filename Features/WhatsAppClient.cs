
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WhatsAppGateway.Features;

public record SendResult(bool Success, string? MessageId, string? Error);

public sealed class WhatsAppClient
{
    private readonly HttpClient _http;
    private readonly WhatsAppOptions _opts;
    private readonly ILogger<WhatsAppClient> _log;

    public WhatsAppClient(HttpClient http, IOptions<WhatsAppOptions> opts, ILogger<WhatsAppClient> log)
    {
        _http = http; _opts = opts.Value; _log = log;
    }

    public async Task<SendResult> SendTextAsync(string to, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opts.PhoneNumberId) || string.IsNullOrWhiteSpace(_opts.AccessToken))
            return new(false, null, "missing_credentials");

        var url = $"{_opts.GraphApiVersion}/{_opts.PhoneNumberId}/messages";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to,
                type = "text",
                text = new { preview_url = false, body = text }
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.AccessToken);

        try
        {
            var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                _log.LogWarning("WhatsApp API {Status}: {Body}", (int)res.StatusCode, body);
                return new(false, null, body);
            }

            string? id = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("messages", out var m) && m.GetArrayLength() > 0)
                    id = m[0].GetProperty("id").GetString();
            } catch { }

            _log.LogInformation("WhatsApp sent to {To}, id={Id}", to, id);
            return new(true, id, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "WhatsApp send exception");
            return new(false, null, ex.Message);
        }
    }
}

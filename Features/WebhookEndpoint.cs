
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace WhatsAppGateway.Features;

public static class WebhookEndpoint
{
    public static IEndpointRouteBuilder MapWebhook(this IEndpointRouteBuilder app)
    {
        // 1) تحقق Meta عند التسجيل
        app.MapGet("/webhook", (HttpContext ctx, IOptions<WhatsAppOptions> opts) =>
        {
            var mode = ctx.Request.Query["hub.mode"];
            var token = ctx.Request.Query["hub.verify_token"];
            var challenge = ctx.Request.Query["hub.challenge"];

            if (mode == "subscribe" && token == opts.Value.VerifyToken)
                return Results.Text(challenge.ToString() ?? "", "text/plain");

            return Results.Unauthorized();
        });

        // 2) استقبال الرسائل
        app.MapPost("/webhook", async (
            HttpContext ctx,
            IOptions<WhatsAppOptions> opts,
            IMemoryCache cache,
            ILoggerFactory lf,
            CancellationToken ct) =>
        {
            var log = lf.CreateLogger("Webhook");
            var o = opts.Value;

            ctx.Request.EnableBuffering();
            string payload;
            using (var r = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true))
                payload = await r.ReadToEndAsync(ct);
            ctx.Request.Body.Position = 0;

            // تحقق التوقيع
            if (!string.IsNullOrWhiteSpace(o.AppSecret))
            {
                var sig = ctx.Request.Headers["X-Hub-Signature-256"].ToString();
                if (!Verify(payload, sig, o.AppSecret))
                {
                    log.LogWarning("Invalid signature");
                    return Results.Unauthorized();
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (!doc.RootElement.TryGetProperty("entry", out var entries))
                    return Results.Ok();

                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes)) continue;
                    foreach (var ch in changes.EnumerateArray())
                    {
                        if (!ch.TryGetProperty("value", out var v)) continue;
                        if (!v.TryGetProperty("messages", out var msgs)) continue;

                        foreach (var m in msgs.EnumerateArray())
                        {
                            var from = m.TryGetProperty("from", out var f) ? f.GetString() : null;
                            var wamid = m.TryGetProperty("id", out var i) ? i.GetString() : null;
                            var text = m.TryGetProperty("text", out var t) &&
                                       t.TryGetProperty("body", out var b) ? b.GetString() : null;

                            if (string.IsNullOrWhiteSpace(from)) continue;

                            // Idempotency
                            if (!string.IsNullOrWhiteSpace(wamid))
                            {
                                var key = $"wamid:{wamid}";
                                if (cache.TryGetValue(key, out _))
                                {
                                    log.LogInformation("Duplicate skipped {Id}", wamid);
                                    continue;
                                }
                                cache.Set(key, true, TimeSpan.FromHours(24));
                            }

                            log.LogInformation("INBOUND from={From} text={Text} id={Id}", from, text, wamid);

                            // ← نقطة التوسع: أضف هنا منطق الرد أو حفظ في قاعدة البيانات
                        }
                    }
                }
                return Results.Ok();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Webhook parse failed");
                return Results.Ok(); // Meta تعيد المحاولة على غير 200
            }
        });

        return app;
    }

    private static bool Verify(string payload, string sig, string secret)
    {
        if (string.IsNullOrEmpty(sig)) return false;
        try
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = "sha256=" + Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(sig));
        }
        catch { return false; }
    }
}

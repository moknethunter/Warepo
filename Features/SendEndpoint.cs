
using Microsoft.Extensions.Options;

namespace WhatsAppGateway.Features;

public static class SendEndpoint
{
    public static IEndpointRouteBuilder MapSend(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/send", async (
            SendRequest req,
            HttpContext ctx,
            IOptions<WhatsAppOptions> opts,
            WhatsAppClient client,
            CancellationToken ct) =>
        {
            // حماية بمفتاح API
            var provided = ctx.Request.Headers["X-Api-Key"].ToString();
            if (!string.IsNullOrEmpty(opts.Value.ApiKey) && provided != opts.Value.ApiKey)
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.To) || string.IsNullOrWhiteSpace(req.Text))
                return Results.BadRequest(new { error = "to and text are required" });

            var result = await client.SendTextAsync(req.To, req.Text, ct);
            return result.Success
                ? Results.Ok(new { success = true, messageId = result.MessageId })
                : Results.Json(new { success = false, error = result.Error }, statusCode: 502);
        });

        return app;
    }

    public record SendRequest(string To, string Text);
}

using WhatsAppGateway.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection("WhatsApp"));
builder.Services.AddHttpClient<WhatsAppClient>(c =>
{
    c.BaseAddress = new Uri("https://graph.facebook.com/");
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddMemoryCache();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));
app.MapWebhook();
app.MapSend();

app.Run();

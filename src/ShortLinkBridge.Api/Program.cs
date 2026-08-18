using ShortLinkBridge.Api.Endpoints;
using ShortLinkBridge.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<ShortLinksApiClient>((sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["ShortLinks:BaseUrl"]
        ?? throw new InvalidOperationException("ShortLinks:BaseUrl is required.");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("ShortLinks:TimeoutSeconds", 30));
});

builder.Services.AddSingleton<ShortLinkQueueProcessor>();

var app = builder.Build();

app.MapQueueEndpoints();

app.Logger.LogInformation("ShortLinkBridge started on {Urls}", string.Join(", ", app.Urls));

app.Run();

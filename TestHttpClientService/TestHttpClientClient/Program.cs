using System.Text.Json;
using ASEDirectlineClient;
using TestHttpClientClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddProvider(new ASELogProvider());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.IncludeFields = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
    });
builder.Services.AddSingleton<ASEWebSocket>();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

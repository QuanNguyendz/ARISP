using ARI.API;
using ARI.API.Extensions;
using ARI.API.Hubs;
using ARI.API.Middleware;
using ARI.Application;
using ARI.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/arisp_api_log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.AddWebServices();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || true) // always enable swagger for prototype ease of test
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Must be first — rewrites Request.Scheme/Host before any middleware reads them
app.UseForwardedHeaders();

app.UseCors("AllowFrontend");

app.UseStaticFiles();

// Ensure local uploads directory exists and configure to serve static CV files
var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<SessionHub>("/hubs/session");
app.MapHub<AppNotificationHub>("/hubs/app-notifications");

await app.InitialiseDatabaseAsync();

app.Run();

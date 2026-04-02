using System.IO;
using CRM.Server.Data;
using CRM.Server.Models;
using CRM.Server.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Ports / URLs: optional config.json next to the project or repo root (see ../../config.json).
foreach (var relative in new[] { Path.Combine("..", "config.json"), "config.json" })
{
    var full = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, relative));
    if (File.Exists(full))
        builder.Configuration.AddJsonFile(full, optional: true, reloadOnChange: true);
}

var urls = builder.Configuration["crmServer:urls"];
if (string.IsNullOrWhiteSpace(urls))
    // Avoid 5000/5001: on Windows they are often in the excluded port range (Hyper-V, etc.) → SocketException 10013.
    urls = "http://localhost:8104;https://localhost:8105";
builder.WebHost.UseUrls(urls);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=crm;Username=postgres;Password=abc123";

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<TicketStatus>("ticket_status");
dataSourceBuilder.MapEnum<TicketPriority>("ticket_priority");
dataSourceBuilder.MapEnum<ImplementationWorkflowStatus>("implementation_status_enum");
var npgsqlDataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(npgsqlDataSource);

builder.Services.AddDbContext<CrmDbContext>(options =>
    options.UseNpgsql(npgsqlDataSource).UseSnakeCaseNamingConvention());

builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IReferenceService, ReferenceService>();
builder.Services.AddHttpClient("PostalPincode", c =>
{
    c.BaseAddress = new Uri("https://api.postalpincode.in/");
    c.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<IPincodeGeoService, PincodeGeoService>();
builder.Services.AddScoped<ITrademarkService, TrademarkService>();
builder.Services.AddScoped<IInvestmentService, InvestmentService>();
builder.Services.AddScoped<ISchedulerService, SchedulerService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CRM API",
        Version = "v1",
        Description = "CRM API aligned with PostgreSQL schema (locations, audit fields, reference-based customer type).",
        Contact = new OpenApiContact
        {
            Name = "CRM Development Team",
            Email = "support@crm.local"
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddLogging();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CRM API v1");
        // Default RoutePrefix is "swagger" → https://localhost:8105/swagger
    });
}

// In Development, HTTP (e.g. http://localhost:8104) must not redirect to HTTPS or
// browser POST/fetch + CORS from the Vite app often breaks.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();
// CORS must run early; with endpoint routing, policies also attach via RequireCors on MapControllers.
app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers().RequireCors("AllowAll");
// No wwwroot/index.html in repo by default — bare GET / would 404. In dev, send browsers to Swagger.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/swagger"));
}
app.MapFallbackToFile("index.html");

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("  CRM Server");
        Console.WriteLine($"  Environment:   {app.Environment.EnvironmentName}");
        Console.WriteLine($"  Content root:  {app.Environment.ContentRootPath}");
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        Console.WriteLine("  Listening on:");
        if (addresses?.Addresses is { Count: > 0 })
        {
            foreach (var address in addresses.Addresses.OrderBy(static a => a))
                Console.WriteLine($"    {address}");
        }
        else
        {
            var fallback = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
                ?? app.Configuration["urls"]
                ?? "http://localhost:8104 (Kestrel default if not configured)";
            Console.WriteLine($"    {fallback}");
        }

        if (app.Environment.IsDevelopment())
        {
            Console.WriteLine("  Swagger UI:    /swagger (append to any listening URL above)");
            Console.WriteLine("  React UI (dev): run core-crm-suite `npm run dev` — default http://localhost:5173 (proxies /api → this server)");
        }
        else
            Console.WriteLine("  Web app / SPA: / when wwwroot/index.html is published");
        Console.WriteLine("========================================");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CRM Server] Could not print listening addresses: {ex.Message}");
    }
});

app.Run();

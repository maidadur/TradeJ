using Microsoft.EntityFrameworkCore;
using TradeJ.Data;
using TradeJ.Services;

namespace TradeJ;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // EF Core – SQLite
        var dbPath = builder.Configuration.GetConnectionString("Default")
            ?? "Data Source=tradej.db";
        builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(dbPath));

        // Services
        builder.Services.AddScoped<DashboardService>();
        builder.Services.AddScoped<MT5LiveImportService>();
        builder.Services.AddScoped<MT5BridgeImportService>();
        builder.Services.AddScoped<CTraderApiService>();
        builder.Services.AddSingleton<AppSettingsService>();
        builder.Services.AddSingleton<MT5AutoSyncService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<MT5AutoSyncService>());
        builder.Services.AddSingleton<CTraderAutoSyncService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<CTraderAutoSyncService>());
        builder.Services.AddHttpClient();

        // Controllers
        builder.Services.AddControllers();

        // OpenAPI / Swagger
        builder.Services.AddOpenApi();

        // CORS – allow Angular dev server
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Angular", policy =>
                policy.WithOrigins(
                        "http://localhost:4200",
                        "https://localhost:4200")
                      .AllowAnyHeader()
                      .AllowAnyMethod());
        });

        var app = builder.Build();

        // Auto-apply migrations on startup
        using (var scope = app.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ctx.Database.MigrateAsync();
        } 

        // Seed default app settings (singleton, resolved from root provider)
        await app.Services.GetRequiredService<AppSettingsService>().InitializeAsync();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/openapi/v1.json", "TradeJ API v1");
                c.RoutePrefix = "swagger";
            });
        }

        app.UseCors("Angular");
        // Only redirect to HTTPS when running outside a container (dev / IIS).
        // In Docker the container speaks plain HTTP; TLS is terminated by the router/nginx.
        if (!app.Environment.IsProduction())
            app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
    }
}

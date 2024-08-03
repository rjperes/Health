using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Mime;
using System.Text.Json;

namespace Health
{
    class TestDbContext : DbContext
    {
        public DbSet<WeatherForecast> WeatherForecasts { get; set; }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddDbContext<TestDbContext>();

            builder.Configuration.GetConnectionString("");

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddHealthChecks()
                .AddManualHealthCheck()
                //.Add(new HealthCheckRegistration("Ping Check", new PingHealthCheck("8.8.8.8"), HealthStatus.Unhealthy, Array.Empty<string>()) { Delay = TimeSpan.FromMinutes(1), Period = TimeSpan.FromMinutes(5) })
                .AddDbContext<TestDbContext>("DbContext Check", ctx => true)
                .AddTypeActivatedCheck<DbContextHealthCheck<TestDbContext>>("Blogs Check", (TestDbContext ctx) => ctx.WeatherForecasts.Any())
                .AddCheck("Web Check", new WebHealthCheck("https://google.com"))
                .AddCheck("Ping Check", new PingHealthCheck("8.8.8.8"))
                .AddCheck("Sample Check", () => HealthCheckResult.Healthy("All is well"))
                .AddAsyncCheck("Sample Async Check", async () => await Task.FromResult(HealthCheckResult.Degraded("All is well")))
                .AddCheck("CPU Usage Check", new CpuUsageHealthCheck());

            builder.Services.Configure<HealthClientOptions>(options =>
            {

            });

            builder.Services.AddHealthClient("https://localhost:7268/Health");

            builder.Services.Configure<HealthCheckPublisherOptions>(options =>
            {
                options.Delay = TimeSpan.FromSeconds(10);
            });

            builder.Services.AddSingleton<IHealthCheckPublisher, PeriodicHealthCheckPublisher>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.MapHealthChecks("/Health", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("db"),
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                },
                AllowCachingResponses = false,
                ResponseWriter = async (context, report) =>
                {
                    var result = JsonSerializer.Serialize(new
                    {
                        report.Entries.Count,
                        Unhealthy = report.Entries.Count(x => x.Value.Status == HealthStatus.Unhealthy),
                        Degraded = report.Entries.Count(x => x.Value.Status == HealthStatus.Degraded),
                        Status = report.Status.ToString(),
                        report.TotalDuration,
                        Checks = report.Entries.Select(e => new
                        {
                            Check = e.Key,
                            e.Value.Description,
                            e.Value.Duration,
                            Status = e.Value.Status.ToString()
                        })
                    });

                    context.Response.Headers["X-Health-Status"] = report.Status.ToString();
                    context.Response.ContentType = MediaTypeNames.Application.Json;

                    await context.Response.WriteAsync(result);
                }
            }).RequireHost("localhost");

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}

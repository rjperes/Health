using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Mime;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace Health
{
    public class PingHealthCheck : IHealthCheck
    {
        public PingHealthCheck(string ipAddress)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress, nameof(ipAddress));

            if (!System.Net.IPAddress.TryParse(ipAddress, out var ip))
            {
                throw new ArgumentException($"Invalid IP address {ipAddress}", nameof(ipAddress));
            }

            this.IPAddress = ip;
        }

        public IPAddress IPAddress { get; }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(IPAddress);

            if (reply.Status == IPStatus.Success)
            {
                return HealthCheckResult.Healthy("The IP address is reachable.");
            }

            return HealthCheckResult.Unhealthy("The IP address is unreachable.");
        }
    }

    public class WebHealthCheck : IHealthCheck
    {
        public WebHealthCheck(string url)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url, nameof(url));

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException($"Invalid URL {url}", nameof(url));
            }

            this.Url = uri;
        }

        public Uri Url { get; }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            var client = new HttpClient();
            var response = await client.GetAsync(this.Url);

            if (response.StatusCode < HttpStatusCode.BadRequest)
            {
                return HealthCheckResult.Healthy("The URL is up and running.");
            }

            return HealthCheckResult.Unhealthy("The URL is inaccessible.");
        }
    }

    public class CpuUsageHealthCheck : IHealthCheck
    {
        class CpuUsageEventListener : EventListener
        {
            private ManualResetEvent? _event = new ManualResetEvent(false);
            private double? _cpuUsage;

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "System.Runtime")
                {
                    EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All);
                }
            }

            public override void Dispose()
            {
                if (_event != null)
                {
                    _event.Dispose();
                    _event = null;
                }

                base.Dispose();
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (eventData.EventName == "EventCounters")
                {
                    foreach (var payload in eventData.Payload!)
                    {
                        if (payload is IDictionary<string, object> eventPayload)
                        {
                            if (eventPayload.TryGetValue("Name", out var counterName) && counterName.ToString() == "cpu-usage")
                            {
                                if (eventPayload.TryGetValue("Mean", out var cpuUsage) && cpuUsage is double)
                                {
                                    _cpuUsage = (double)cpuUsage;
                                    _event!.Set();
                                }
                            }
                        }
                    }
                }
            }

            public double? GetCpuUsage()
            {
                if (_event == null)
                {
                    throw new ObjectDisposedException("This object has been disposed.");
                }

                var eventSource = EventSource.GetSources().Single(x => x.Name == "System.Runtime");
                EnableEvents(eventSource, EventLevel.LogAlways);
                _event!.WaitOne();
                DisableEvents(eventSource);
                return _cpuUsage;
            }
        }

        public const double DefaultCpuUsageLimit = 0.8;

        public CpuUsageHealthCheck(double cpuUsageLimit = DefaultCpuUsageLimit)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cpuUsageLimit, nameof(cpuUsageLimit));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cpuUsageLimit, 1, nameof(cpuUsageLimit));

            this.CpuUsageLimit = cpuUsageLimit;
        }

        public double CpuUsageLimit { get; }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            using var listener = new CpuUsageEventListener();
            var cpuUsage = listener.GetCpuUsage();

            if (cpuUsage >= CpuUsageLimit)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("High CPU usage."));
            }

            return Task.FromResult(HealthCheckResult.Healthy("CPU usage is normal."));
        }
    }

    class PeriodicHealthCheckPublisher : IHealthCheckPublisher
    {
        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
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

            Console.WriteLine(result);

            return Task.CompletedTask;
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddHealthChecks()
                //.Add(new HealthCheckRegistration { })
                .AddCheck("Web Check", new WebHealthCheck("https://google.com"))
                .AddCheck("Ping Check", new PingHealthCheck("8.8.8.8"))
                .AddCheck("Sample Check", () => HealthCheckResult.Healthy("All is well"))
                .AddAsyncCheck("Sample Async Check", async () => await Task.FromResult(HealthCheckResult.Healthy("All is well")))
                .AddCheck("CPU Usage Check", new CpuUsageHealthCheck());

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

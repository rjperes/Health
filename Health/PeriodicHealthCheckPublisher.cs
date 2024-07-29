using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Health
{
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
}

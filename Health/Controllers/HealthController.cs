﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Health.Controllers
{
    public class HealthController : Controller
    {
        [HttpGet("[action]")]
        public async Task<IActionResult> CheckHealth([FromServices] HealthCheckService healthService, CancellationToken cancellationToken)
        {
            var report = await healthService.CheckHealthAsync(registrations => registrations.Tags.Contains("tcp"), cancellationToken);

            var result = new
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
            };


            return Json(result);
        }
    }
}

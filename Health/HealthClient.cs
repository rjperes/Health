using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;

namespace Health
{
    public interface IHealthClient
    {
        Task<HealthStatus> IsHealthy(string url, CancellationToken cancellationToken = default);
    }

    public class HealthClient : IHealthClient
    {
        private readonly HttpClient _httpClient;

        public HealthClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<HealthStatus> IsHealthy(string url = "/Health", CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            return response.StatusCode == HttpStatusCode.ServiceUnavailable ? HealthStatus.Unhealthy : HealthStatus.Healthy;
        }
    }
}

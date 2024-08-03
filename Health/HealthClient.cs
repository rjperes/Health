using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Health
{
    public interface IHealthClient
    {
        Task<HealthStatus> CheckHealth(CancellationToken cancellationToken = default);
    }

    public class HealthClientOptions
    {
        public Dictionary<HealthStatus, int> ResultStatusCodes { get; } = new Dictionary<HealthStatus, int>
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        };
    }

    public class HealthClient : IHealthClient
    {
        private readonly HttpClient _httpClient;
        private readonly HealthClientOptions _options;

        public HealthClient(HttpClient httpClient, IOptions<HealthClientOptions>? options)
        {
            ArgumentNullException.ThrowIfNull(httpClient, nameof(httpClient));

            _httpClient = httpClient;
            _options = options?.Value ?? new HealthClientOptions();
        }

        public async Task<HealthStatus> CheckHealth(CancellationToken cancellationToken = default)
        {
            var response = await _httpClient.GetAsync(string.Empty, cancellationToken);

            var results = _options.ResultStatusCodes.Where(x => x.Value == (int)response.StatusCode);

            if (results.Count() == 1)
            {
                return results.Single().Key;
            }
            else if (results.Count() > 1)
            {
                if (response.Headers.TryGetValues("X-Health-Status", out var header) && Enum.TryParse<HealthStatus>(header.First(), out var status))
                {
                    return status;
                }
            }

            if (response.IsSuccessStatusCode)
            {
                return HealthStatus.Healthy;
            }

            return HealthStatus.Unhealthy;
        }
    }

    public class LocalHealthClient : IHealthClient
    {
        private readonly HealthCheckService _healthService;

        public LocalHealthClient(HealthCheckService healthService)
        {
            _healthService = healthService;
        }

        public async Task<HealthStatus> CheckHealth(CancellationToken cancellationToken = default)
        {
            var result = await _healthService.CheckHealthAsync(cancellationToken);
            return result.Status;
        }
    }
}

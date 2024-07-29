using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.NetworkInformation;

namespace Health
{
    public class DbContextHealthCheck<TContext> : IHealthCheck where TContext : DbContext
    {
        public DbContextHealthCheck(TContext context, Func<TContext, bool> condition)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));
            ArgumentNullException.ThrowIfNull(condition, nameof(condition));

            Context = context;
            Condition = condition;
        }

        public TContext Context { get; }
        public Func<TContext, bool> Condition { get; }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                if (Condition(Context))
                {
                    return Task.FromResult(HealthCheckResult.Healthy("Query succeeded."));
                }
                else
                {
                    return Task.FromResult(HealthCheckResult.Unhealthy("Query failed."));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Query failed.", ex));
            }
        }
    }

    public class SqlServerHealthCheck : IHealthCheck
    {
        public SqlServerHealthCheck(string connectionString)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using var con = new SqlConnection(ConnectionString);
                using var cmd = con.CreateCommand();

                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync();

                return HealthCheckResult.Healthy("Connection successful.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Connection failed.", ex);
            }
        }
    }

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
            using var client = new HttpClient();
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

            if (cpuUsage > CpuUsageLimit)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("High CPU usage."));
            }

            return Task.FromResult(HealthCheckResult.Healthy("CPU usage is normal."));
        }
    }

}

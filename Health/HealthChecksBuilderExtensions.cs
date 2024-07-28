using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Health
{
    public static class HealthChecksBuilderExtensions
    {
        public static IHealthChecksBuilder AddWeb(this IHealthChecksBuilder builder, string name, string url, IEnumerable<string>? tags = null)
        {
            return builder.AddCheck(name, new WebHealthCheck(url), tags: tags);
        }

        public static IHealthChecksBuilder AddPing(this IHealthChecksBuilder builder, string name, string ipAddress, IEnumerable<string>? tags = null)
        {
            return builder.AddCheck(name, new PingHealthCheck(ipAddress), tags: tags);
        }

        public static IHealthChecksBuilder AddCpuUsageLimit(this IHealthChecksBuilder builder, string name, double cpuUsageLimit = CpuUsageHealthCheck.DefaultCpuUsageLimit, IEnumerable<string>? tags = null)
        {
            return builder.AddCheck(name, new CpuUsageHealthCheck(cpuUsageLimit), tags: tags);
        }

        public static IHealthChecksBuilder AddSqlServer(this IHealthChecksBuilder builder, string name, string connectionString, IEnumerable<string>? tags = null)
        {
            return builder.AddCheck(name, new SqlServerHealthCheck(connectionString), tags: tags);
        }

        public static IHealthChecksBuilder AddDbContext<TContext>(this IHealthChecksBuilder builder, string name, Func<TContext, bool> condition, IEnumerable<string>? tags = null) where TContext : DbContext
        {
            return builder.AddTypeActivatedCheck<DbContextHealthCheck<TContext>>(name, HealthStatus.Unhealthy, args: condition, tags: tags);
        }
    }
}

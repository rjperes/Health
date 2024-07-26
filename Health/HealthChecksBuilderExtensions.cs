using Microsoft.EntityFrameworkCore;

namespace Health
{
    public static class HealthChecksBuilderExtensions
    {
        public static IHealthChecksBuilder AddWeb(this IHealthChecksBuilder builder, string name, string url)
        {
            return builder.AddCheck(name, new WebHealthCheck(url));
        }

        public static IHealthChecksBuilder AddPing(this IHealthChecksBuilder builder, string name, string ipAddress)
        {
            return builder.AddCheck(name, new PingHealthCheck(ipAddress));
        }

        public static IHealthChecksBuilder AddCpuUsageLimit(this IHealthChecksBuilder builder, string name, double cpuUsageLimit = CpuUsageHealthCheck.DefaultCpuUsageLimit)
        {
            return builder.AddCheck(name, new CpuUsageHealthCheck(cpuUsageLimit));
        }

        public static IHealthChecksBuilder AddSqlServer(this IHealthChecksBuilder builder, string name, string connectionString)
        {
            return builder.AddCheck(name, new SqlServerHealthCheck(connectionString));
        }

        public static IHealthChecksBuilder AddDbContext<TContext>(this IHealthChecksBuilder builder, string name, Func<TContext, bool> condition) where TContext : DbContext
        {
            return builder.AddTypeActivatedCheck<DbContextHealthCheck<TContext>>(name, condition);
        }
    }
}

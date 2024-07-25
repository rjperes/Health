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
    }
}

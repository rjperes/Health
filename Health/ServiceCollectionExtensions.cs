using Microsoft.Net.Http.Headers;
using System.Net.Mime;

namespace Health
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHealthCheckClient(this IServiceCollection services, string baseAddress)
        {
            services.AddHttpClient("HealthCheckClient", client =>
            {
                client.BaseAddress = new Uri(baseAddress);
                client.DefaultRequestHeaders.Add(HeaderNames.Accept, MediaTypeNames.Application.Json);
                client.DefaultRequestHeaders.Add(HeaderNames.CacheControl, "no-cache");
            }).AddTypedClient<IHealthCheckClient, HealthCheckClient>();

            return services;
        }
    }
}

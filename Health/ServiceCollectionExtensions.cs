using Microsoft.Net.Http.Headers;
using System.Net.Mime;

namespace Health
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHealthClient(this IServiceCollection services, string baseAddress)
        {
            services.AddHttpClient("HealthClient", client =>
            {
                client.BaseAddress = new Uri(baseAddress);
                client.DefaultRequestHeaders.Add(HeaderNames.Accept, MediaTypeNames.Application.Json);
                client.DefaultRequestHeaders.Add(HeaderNames.CacheControl, "no-cache");
            }).AddTypedClient<IHealthClient, HealthClient>();

            return services;
        }
    }
}

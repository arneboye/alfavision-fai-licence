using Microsoft.Extensions.DependencyInjection;

namespace AlfaVision.FaiLicence;

/// <summary>Dependency-injection registration.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FaiLicenceClient"/> with a typed <see cref="HttpClient"/> (20 s timeout) and the given options.
    /// <para>
    /// Request logging is removed on purpose: the FAI API takes the credentials as query-string parameters, and the
    /// default <c>HttpClient</c> logging writes the full request URL at Information level — which would put the
    /// user name and the (base64-encoded) password into your logs.
    /// </para>
    /// </summary>
    public static IServiceCollection AddFaiLicenceClient(this IServiceCollection services, Action<FaiLicenceOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<FaiLicenceClient>(c => c.Timeout = TimeSpan.FromSeconds(20)).RemoveAllLoggers();
        return services;
    }
}

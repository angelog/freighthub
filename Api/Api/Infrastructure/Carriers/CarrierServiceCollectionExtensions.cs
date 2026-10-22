using Api.Domain.Carriers;

namespace Api.Infrastructure.Carriers;

public static class CarrierServiceCollectionExtensions
{
    /// <summary>
    /// Registra os providers de transportadora <b>disponíveis</b> (instalados):
    /// Correios/Loggi ficam disponíveis quando têm BaseUrl configurada. O liga/desliga
    /// em runtime é decidido pelo <see cref="ICarrierSettingsStore"/> (tabela
    /// carrier_settings), não aqui — assim dá para desativar uma transportadora sem
    /// redeploy e sem afetar as outras. Os clients HTTP ganham o pipeline de resiliência.
    /// </summary>
    public static IServiceCollection AddCarriers(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CarrierOptions.SectionName);
        services.Configure<CarrierOptions>(section);
        var options = section.Get<CarrierOptions>() ?? new CarrierOptions();

        services.AddScoped<ICarrierSettingsStore, CarrierSettingsStore>();
        services.AddScoped<CarrierQuoteAggregator>();

        if (!string.IsNullOrWhiteSpace(options.Correios.BaseUrl))
        {
            services.AddHttpClient<CorreiosCarrierProvider>(client =>
                    client.BaseAddress = new Uri(options.Correios.BaseUrl))
                .AddStandardResilienceHandler(CarrierResilience.Configure);

            services.AddTransient<ICarrierProvider>(sp => sp.GetRequiredService<CorreiosCarrierProvider>());
        }

        if (!string.IsNullOrWhiteSpace(options.Loggi.BaseUrl))
        {
            services.AddHttpClient<LoggiCarrierProvider>(client =>
                    client.BaseAddress = new Uri(options.Loggi.BaseUrl))
                .AddStandardResilienceHandler(CarrierResilience.Configure);

            services.AddTransient<ICarrierProvider>(sp => sp.GetRequiredService<LoggiCarrierProvider>());
        }

        if (!string.IsNullOrWhiteSpace(options.Fretops.BaseUrl))
        {
            services.AddHttpClient<FretopsCarrierProvider>(client =>
                    client.BaseAddress = new Uri(options.Fretops.BaseUrl))
                .AddStandardResilienceHandler(CarrierResilience.ConfigureFretops);

            services.AddTransient<ICarrierProvider>(sp => sp.GetRequiredService<FretopsCarrierProvider>());
        }

        return services;
    }
}

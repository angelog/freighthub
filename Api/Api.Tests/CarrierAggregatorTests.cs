using Api.Domain.Carriers;
using Api.Infrastructure.Carriers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public class CarrierAggregatorTests
{
    private static CarrierQuoteRequest Request() => new()
    {
        OriginZipCode = "01001000",
        DestinationZipCode = "04543000",
        TotalWeightKg = 1m,
        DeclaredValue = 100m
    };

    [Fact]
    public async Task Only_enabled_carriers_are_queried()
    {
        var loggi = new StubCarrierProvider(CarrierType.Loggi, "Loggi Econômico", 11.71m);
        var correios = new StubCarrierProvider(CarrierType.Correios, "SEDEX", 20m);

        var settings = new FakeCarrierSettingsStore(new()
        {
            [CarrierType.Loggi] = false, // desligada
            [CarrierType.Correios] = true
        });

        var aggregator = new CarrierQuoteAggregator(
            [loggi, correios], settings, NullLogger<CarrierQuoteAggregator>.Instance);

        var quotes = await aggregator.GetQuotesAsync(Request());

        Assert.Single(quotes);
        Assert.Equal(CarrierType.Correios, quotes[0].Carrier);
        Assert.False(loggi.WasCalled);   // desligada não foi chamada
        Assert.True(correios.WasCalled);
    }

    [Fact]
    public async Task Failing_carrier_does_not_break_the_others()
    {
        var failing = new StubCarrierProvider(CarrierType.Correios, "x", 0m, throws: true);
        var ok = new StubCarrierProvider(CarrierType.Loggi, "Loggi", 11.71m);

        var settings = new FakeCarrierSettingsStore(new()
        {
            [CarrierType.Correios] = true,
            [CarrierType.Loggi] = true
        });

        var aggregator = new CarrierQuoteAggregator(
            [failing, ok], settings, NullLogger<CarrierQuoteAggregator>.Instance);

        var quotes = await aggregator.GetQuotesAsync(Request());

        Assert.Single(quotes);
        Assert.Equal(CarrierType.Loggi, quotes[0].Carrier);
    }

    [Fact]
    public async Task No_enabled_carriers_returns_empty()
    {
        var correios = new StubCarrierProvider(CarrierType.Correios, "SEDEX", 20m);
        var settings = new FakeCarrierSettingsStore(new() { [CarrierType.Correios] = false });

        var aggregator = new CarrierQuoteAggregator(
            [correios], settings, NullLogger<CarrierQuoteAggregator>.Instance);

        var quotes = await aggregator.GetQuotesAsync(Request());

        Assert.Empty(quotes);
        Assert.False(correios.WasCalled);
    }
}

internal sealed class StubCarrierProvider(
    CarrierType carrier, string serviceName, decimal price, bool throws = false) : ICarrierProvider
{
    public bool WasCalled { get; private set; }
    public CarrierType Carrier => carrier;

    public Task<IReadOnlyList<CarrierQuote>> GetQuotesAsync(
        CarrierQuoteRequest request, CancellationToken cancellationToken = default)
    {
        WasCalled = true;
        if (throws)
            throw new InvalidOperationException("falha simulada");

        IReadOnlyList<CarrierQuote> quotes =
        [
            new CarrierQuote { Carrier = carrier, ServiceName = serviceName, Price = price, EstimatedDays = 3 }
        ];
        return Task.FromResult(quotes);
    }
}

internal sealed class FakeCarrierSettingsStore(Dictionary<CarrierType, bool> states) : ICarrierSettingsStore
{
    public Task<IReadOnlyDictionary<CarrierType, bool>> GetEnabledStatesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<CarrierType, bool>>(states);

    public Task<bool> IsEnabledAsync(CarrierType carrier, CancellationToken cancellationToken = default)
        => Task.FromResult(states.TryGetValue(carrier, out var on) && on);

    public Task SetEnabledAsync(CarrierType carrier, bool isEnabled, CancellationToken cancellationToken = default)
    {
        states[carrier] = isEnabled;
        return Task.CompletedTask;
    }
}

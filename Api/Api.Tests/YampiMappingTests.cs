using Api.Application.Yampi;
using Api.Domain.Carriers;
using Api.Domain.Rules;
using Api.Infrastructure.Yampi;

namespace Api.Tests;

public class YampiMappingTests
{
    private static YampiQuoteRequest SampleRequest() => new()
    {
        Zipcode = "04543-000",
        Amount = 120m,
        Skus =
        [
            new YampiSku { UnitPrice = 60m, Quantity = 2, Weight = 1.5m, Length = 20, Width = 15, Height = 10 }
        ]
    };

    [Fact]
    public void ToShippingContext_builds_cart_from_skus()
    {
        var ctx = YampiMapping.ToShippingContext(SampleRequest(), DateTimeOffset.UtcNow);

        Assert.Equal("04543000", ctx.DestinationZipCode); // normalizado
        Assert.Equal(120m, ctx.Cart.TotalValue);          // 60 x 2
        Assert.Equal(3.0m, ctx.Cart.TotalWeightKg);       // 1.5 x 2
    }

    [Fact]
    public void CarrierQuotes_are_ordered_by_price_with_random_ids()
    {
        IReadOnlyList<CarrierQuote> carrier =
        [
            new()
            {
                Carrier = CarrierType.Correios, ServiceName = "SEDEX", ExternalServiceCode = "03220",
                Price = 30m, EstimatedDays = 2
            },
            new() { Carrier = CarrierType.Loggi, ServiceName = "Loggi Econômico", Price = 11.71m, EstimatedDays = 1 }
        ];

        var quotes = YampiMapping.CarrierQuotes(carrier);

        Assert.Equal("Loggi", quotes[0].Name);              // mais barato primeiro, nome = transportadora
        Assert.Equal("ECONOMICO_LOGGI", quotes[0].Service); // código = SERVICO_TRANSPORTADORA
        Assert.Equal("SEDEX", quotes[1].Name);               // Correios: nome = só o serviço
        Assert.Equal("CONTRATO_03220", quotes[1].Service);   // Correios: serviço = CONTRATO_{código}
        Assert.InRange(quotes[0].QuoteId, 1_000_000_000, long.MaxValue); // id aleatório de ~10+ dígitos
        Assert.InRange(quotes[1].QuoteId, 1_000_000_000, long.MaxValue);
        Assert.NotEqual(quotes[0].QuoteId, quotes[1].QuoteId);
        Assert.All(quotes, q => Assert.False(q.FreeShipment));
    }

    [Fact]
    public void CarrierQuotes_correios_uses_service_name_and_contract_code()
    {
        IReadOnlyList<CarrierQuote> carrier =
        [
            new()
            {
                Carrier = CarrierType.Correios, ServiceName = "PAC", ExternalServiceCode = "03298",
                Price = 18m, EstimatedDays = 6
            },
            new()
            {
                Carrier = CarrierType.Correios, ServiceName = "SEDEX", ExternalServiceCode = "03220",
                Price = 32m, EstimatedDays = 2
            },
            new() { Carrier = CarrierType.Loggi, ServiceName = "Loggi Econômico", Price = 12m, EstimatedDays = 1 }
        ];

        var quotes = YampiMapping.CarrierQuotes(carrier);

        // Loggi tem uma opção só → nome = transportadora
        Assert.Equal("Loggi", quotes.Single(q => q.Service == "ECONOMICO_LOGGI").Name);
        // Correios: nome = só o serviço, serviço = CONTRATO_{código}, mesmo com várias opções.
        Assert.Contains("PAC", quotes.Where(q => q.Service == "CONTRATO_03298").Select(q => q.Name));
        Assert.Contains("SEDEX", quotes.Where(q => q.Service == "CONTRATO_03220").Select(q => q.Name));
        Assert.Equal(3, quotes.Select(q => q.Service).Distinct().Count()); // todos únicos
    }

    [Fact]
    public void CarrierQuotes_correios_without_external_code_falls_back_to_service_name()
    {
        IReadOnlyList<CarrierQuote> carrier =
        [
            new() { Carrier = CarrierType.Correios, ServiceName = "PAC", Price = 18m, EstimatedDays = 6 }
        ];

        var quotes = YampiMapping.CarrierQuotes(carrier);

        Assert.Equal("PAC", quotes[0].Service); // sem código externo → usa o nome do serviço
    }

    [Fact]
    public void CarrierQuotes_fretops_passes_through_name_and_service_as_received()
    {
        // A Fretops manda name/service prontos na resposta (ex.: "Expressa"/"FRETOPS");
        // devolvemos exatamente como veio, sem reformatar como SERVICO_TRANSPORTADORA
        // (diferente de Loggi, que não tem esse conceito na própria API).
        IReadOnlyList<CarrierQuote> carrier =
        [
            new()
            {
                Carrier = CarrierType.Fretops, ServiceName = "Expressa", ExternalServiceCode = "FRETOPS",
                Price = 12.16m, EstimatedDays = 1
            }
        ];

        var quotes = YampiMapping.CarrierQuotes(carrier);

        Assert.Equal("Expressa", quotes[0].Name);
        Assert.Equal("FRETOPS", quotes[0].Service);
    }

    [Fact]
    public void CarrierQuotes_fretops_without_external_service_falls_back_to_carrier_name()
    {
        IReadOnlyList<CarrierQuote> carrier =
        [
            new() { Carrier = CarrierType.Fretops, ServiceName = "Expressa", Price = 12.16m, EstimatedDays = 1 }
        ];

        var quotes = YampiMapping.CarrierQuotes(carrier);

        Assert.Equal("Expressa", quotes[0].Name);
        Assert.Equal("FRETOPS", quotes[0].Service); // sem código externo -> cai no nome da transportadora
    }

    [Fact]
    public void CarrierQuotes_preserves_external_quote_id_from_carrier()
    {
        // A Fretops atribui o próprio id (checkout_ref) à cotação; precisa ser
        // devolvido à Yampi sem alteração, não substituído por um id gerado aqui.
        IReadOnlyList<CarrierQuote> carrier =
        [
            new()
            {
                Carrier = CarrierType.Fretops, ServiceName = "Expressa", Price = 12.16m, EstimatedDays = 1,
                ExternalQuoteId = 963329028480437
            },
            new() { Carrier = CarrierType.Loggi, ServiceName = "Loggi Econômico", Price = 20m, EstimatedDays = 2 }
        ];

        var quotes = YampiMapping.CarrierQuotes(carrier);

        Assert.Equal(963329028480437, quotes[0].QuoteId);       // Fretops: id preservado
        Assert.InRange(quotes[1].QuoteId, 1_000_000_000, long.MaxValue); // Loggi: sem id externo, gera um
    }

    [Fact]
    public void RuleFixedQuote_preserves_external_quote_id_when_cheapest_is_fretops()
    {
        var result = RuleResult.FixedPrice(15m);
        var applied = new AppliedRule(Guid.NewGuid(), "Sudeste R$15", "MinimumOrderFixed", 1, result);
        var evaluation = new RuleEvaluation(result, [applied]);
        var cheapest = new CarrierQuote
        {
            Carrier = CarrierType.Fretops, ServiceName = "Expressa", Price = 12.16m, EstimatedDays = 1,
            ExternalQuoteId = 963329028480437
        };
        var fallback = new YampiFallback { Service = "CORREIOS_PAC", Days = 7 };

        var quote = YampiMapping.RuleFixedQuote(evaluation, cheapest, fallback);

        Assert.Equal(963329028480437, quote.QuoteId);
    }

    [Fact]
    public void FreeShippingQuote_preserves_external_quote_id_when_cheapest_is_fretops()
    {
        var cheapest = new CarrierQuote
        {
            Carrier = CarrierType.Fretops, ServiceName = "Expressa", Price = 12.16m, EstimatedDays = 1,
            ExternalQuoteId = 963329028480437
        };
        var fallback = new YampiFallback { Service = "PADRAO", Price = 29.90m, Days = 7 };

        var quote = YampiMapping.FreeShippingQuote(cheapest, fallback);

        Assert.Equal(963329028480437, quote.QuoteId);
    }

    [Fact]
    public void RuleFixedQuote_uses_rule_price_but_cheapest_carrier_service()
    {
        var result = RuleResult.FixedPrice(15m);
        var applied = new AppliedRule(Guid.NewGuid(), "Sudeste R$15", "MinimumOrderFixed", 1, result);
        var evaluation = new RuleEvaluation(result, [applied]);
        var cheapest = new CarrierQuote
        {
            Carrier = CarrierType.Loggi, ServiceName = "Loggi Econômico", Price = 12m, EstimatedDays = 2
        };
        var fallback = new YampiFallback { Service = "CORREIOS_PAC", Days = 7 };

        var quote = YampiMapping.RuleFixedQuote(evaluation, cheapest, fallback);

        Assert.False(quote.FreeShipment);
        Assert.Equal(15m, quote.Price);                 // preço vem da regra
        Assert.Equal("ECONOMICO_LOGGI", quote.Service); // identificado pela transportadora mais barata
        Assert.Equal("Sudeste R$15", quote.Name);
        Assert.Equal(2, quote.Days);                    // prazo da transportadora
    }

    [Fact]
    public void RuleFixedQuote_without_carrier_falls_back_to_fallback_service()
    {
        var result = RuleResult.FixedPrice(15m);
        var applied = new AppliedRule(Guid.NewGuid(), "Sudeste R$15", "MinimumOrderFixed", 1, result);
        var evaluation = new RuleEvaluation(result, [applied]);
        var fallback = new YampiFallback { Service = "PAC_CORREIOS", Days = 7 };

        var quote = YampiMapping.RuleFixedQuote(evaluation, cheapest: null, fallback);

        Assert.Equal(15m, quote.Price);
        Assert.Equal("PAC_CORREIOS", quote.Service); // sem transportadora → serviço do fallback
        Assert.Equal(7, quote.Days);
    }

    [Fact]
    public void FreeShippingQuote_is_zero_priced_with_carrier_prazo()
    {
        var cheapest = new CarrierQuote
        {
            Carrier = CarrierType.Loggi, ServiceName = "Loggi Econômico", Price = 11.71m, EstimatedDays = 1
        };
        var fallback = new YampiFallback { Service = "PADRAO", Price = 29.90m, Days = 7 };

        var quote = YampiMapping.FreeShippingQuote(cheapest, fallback);

        Assert.True(quote.FreeShipment);
        Assert.Equal(0m, quote.Price);              // grátis pro cliente
        Assert.Equal(1, quote.Days);                    // prazo da transportadora mais barata
        Assert.Equal("ECONOMICO_LOGGI", quote.Service); // reflete a transportadora+serviço usados
        Assert.Equal("Loggi", quote.Name);              // nome = transportadora
    }

    [Fact]
    public void FreeShippingQuote_without_carrier_uses_fallback_prazo()
    {
        var fallback = new YampiFallback { Name = "Frete padrão", Service = "PADRAO", Price = 29.90m, Days = 7 };

        var quote = YampiMapping.FreeShippingQuote(cheapest: null, fallback);

        Assert.True(quote.FreeShipment);
        Assert.Equal(0m, quote.Price);            // continua grátis
        Assert.Equal(7, quote.Days);              // sem transportadora → prazo de fallback
        Assert.Equal("Frete padrão", quote.Name); // sem transportadora → nome do fallback
    }

    [Fact]
    public void FallbackQuote_maps_configured_values()
    {
        var fallback = new YampiFallback { Name = "Padrão", Service = "PADRAO", Price = 29.90m, Days = 7 };

        var quote = YampiMapping.FallbackQuote(fallback);

        Assert.Equal("Padrão", quote.Name);
        Assert.Equal(29.90m, quote.Price);
        Assert.Equal(7, quote.Days);
        Assert.False(quote.FreeShipment);
    }
}

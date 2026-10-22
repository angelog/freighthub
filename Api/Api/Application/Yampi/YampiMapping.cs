using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Api.Domain.Carriers;
using Api.Domain.Rules;
using Api.Domain.Shipping;
using Api.Infrastructure.Yampi;

namespace Api.Application.Yampi;

/// <summary>
/// Conversões entre o contrato da Yampi e o domínio interno (e vice-versa).
/// Funções puras — fáceis de testar isoladamente.
/// </summary>
public static class YampiMapping
{
    public static ShippingContext ToShippingContext(YampiQuoteRequest request, DateTimeOffset now)
    {
        var items = request.Skus
            .Where(s => s.Quantity > 0)
            .Select(s => new CartItem(
                s.UnitPrice,
                s.Quantity,
                s.Weight,
                s is { Length: > 0, Width: > 0, Height: > 0 }
                    ? new PackageDimensions(s.Length, s.Width, s.Height)
                    : null,
                string.IsNullOrWhiteSpace(s.Sku) ? s.Id.ToString() : s.Sku))
            .ToList();

        var cart = new Cart(items);
        return new ShippingContext(request.Zipcode, cart, now);
    }

    public static CarrierQuoteRequest? ToCarrierRequest(ShippingContext context, string originZipCode)
    {
        if (!Cep.TryNormalize(originZipCode, out var origin))
            return null;

        return new CarrierQuoteRequest
        {
            OriginZipCode = origin,
            DestinationZipCode = context.DestinationZipCode,
            TotalWeightKg = context.Cart.TotalWeightKg,
            DeclaredValue = context.Cart.TotalValue,
            Items = context.Cart.Items
                .Select(i => new CarrierQuoteItem
                {
                    Sku = i.Sku ?? "SKU",
                    Quantity = i.Quantity,
                    WeightG = Math.Max(1, (int)Math.Round(i.WeightKg * 1000m)),
                    LengthCm = i.Dimensions?.LengthCm,
                    WidthCm = i.Dimensions?.WidthCm,
                    HeightCm = i.Dimensions?.HeightCm
                })
                .ToList()
        };
    }

    public static YampiQuote RuleFixedQuote(RuleEvaluation evaluation, CarrierQuote? cheapest, YampiFallback fallback)
    {
        var result = evaluation.Result;
        return new YampiQuote
        {
            Name = evaluation.DecidingRule?.RuleName ?? "Frete",
            Service = cheapest is not null
                ? BuildServiceCode(cheapest)
                : fallback.Service,
            Price = result.Price ?? 0m,
            Days = result.EstimatedDays ?? cheapest?.EstimatedDays ?? fallback.Days,
            QuoteId = cheapest?.ExternalQuoteId ?? GenerateQuoteId(),
            FreeShipment = false
        };
    }

    /// <summary>
    /// Quotes a partir das cotações das transportadoras (ordenadas por preço).
    /// Nome = transportadora (ex.: "Loggi"); quando a mesma transportadora devolve mais
    /// de um serviço (ex.: Loggi Expresso e Econômico), o nome os diferencia
    /// ("Expresso (Loggi)"). Correios é exceção: nome é sempre só o nome do serviço
    /// (ex.: "PAC", "SEDEX") e o serviço é o código do contrato (ex.: "CONTRATO_03298").
    /// </summary>
    public static IReadOnlyList<YampiQuote> CarrierQuotes(IReadOnlyList<CarrierQuote> quotes)
    {
        var countByCarrier = quotes
            .GroupBy(q => q.Carrier)
            .ToDictionary(g => g.Key, g => g.Count());

        return quotes
            .OrderBy(q => q.Price)
            .Select(q => new YampiQuote
            {
                Name = CarrierDisplayName(q.Carrier, q.ServiceName, disambiguate: countByCarrier[q.Carrier] > 1),
                Service = BuildServiceCode(q),
                Price = q.Price,
                Days = q.EstimatedDays,
                QuoteId = q.ExternalQuoteId ?? GenerateQuoteId(),
                FreeShipment = q.Price == 0m
            })
            .ToList();
    }

    /// <summary>
    /// Nome exibido de uma opção: só a transportadora quando ela tem uma opção, ou
    /// "Serviço (Transportadora)" para diferenciar quando há várias — ex.: "SEDEX (Correios)".
    /// Remove o nome da transportadora do rótulo do serviço para não repetir
    /// (ex.: "Loggi Econômico" → "Econômico (Loggi)").
    /// Correios e Fretops são exceção: usam sempre só o nome do serviço como veio da
    /// API delas (ex.: "PAC", "Expressa"), sem parênteses.
    /// </summary>
    private static string CarrierDisplayName(CarrierType carrier, string serviceName, bool disambiguate)
    {
        if (carrier is CarrierType.Correios or CarrierType.Fretops && !string.IsNullOrWhiteSpace(serviceName))
            return serviceName.Trim();

        var carrierName = carrier.ToString();
        if (!disambiguate || string.IsNullOrWhiteSpace(serviceName))
            return carrierName;

        var service = serviceName.Trim();
        if (service.Contains(carrierName, StringComparison.OrdinalIgnoreCase))
        {
            service = service.Replace(carrierName, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            if (string.IsNullOrEmpty(service))
                return carrierName; // o serviço era apenas o nome da transportadora
        }

        return $"{service} ({carrierName})";
    }

    /// <summary>
    /// Frete grátis: uma única opção com <c>price = 0</c> (a Yampi cobra o valor de
    /// <c>price</c>, então tem que ser zero), com o prazo da transportadora mais barata
    /// — que é a que será usada, já que a loja arca com o frete. O nome exibido é a
    /// transportadora (ex.: "Loggi"). Sem transportadora, cai no fallback.
    /// </summary>
    public static YampiQuote FreeShippingQuote(CarrierQuote? cheapest, YampiFallback fallback) => new()
    {
        Name = cheapest is not null ? cheapest.Carrier.ToString() : fallback.Name,
        Service = cheapest is not null
            ? BuildServiceCode(cheapest)
            : fallback.Service,
        Price = 0m,
        Days = cheapest?.EstimatedDays ?? fallback.Days,
        QuoteId = cheapest?.ExternalQuoteId ?? GenerateQuoteId(),
        FreeShipment = true
    };

    public static YampiQuote FallbackQuote(YampiFallback fallback) => new()
    {
        Name = fallback.Name,
        Service = fallback.Service,
        Price = fallback.Price,
        Days = fallback.Days,
        QuoteId = GenerateQuoteId(),
        FreeShipment = fallback.Price == 0m
    };

    /// <summary>
    /// Código de integração único por transportadora+serviço, no formato
    /// <c>SERVICO_TRANSPORTADORA</c> (ex.: <c>ECONOMICO_LOGGI</c>). É este valor que se
    /// mapeia nas "Formas de frete" da Yampi e no "Código na integração" do Olist para
    /// identificar a transportadora no pedido. Quando o nome do serviço já cita a
    /// transportadora, evita duplicá-la.
    /// Correios é exceção: usa o código do serviço na API do contrato, no formato
    /// <c>CONTRATO_{código}</c> (ex.: <c>CONTRATO_03220</c> para SEDEX, <c>CONTRATO_03298</c>
    /// para PAC). Fretops também é exceção: devolve o campo <c>service</c> exatamente
    /// como veio da resposta dela (ex.: <c>FRETOPS</c>), sem reformatar — é o valor
    /// usado no exemplo do manual de integração deles.
    /// </summary>
    private static string BuildServiceCode(CarrierQuote quote)
    {
        if (quote.Carrier == CarrierType.Correios)
        {
            return string.IsNullOrWhiteSpace(quote.ExternalServiceCode)
                ? Slugify(quote.ServiceName)
                : $"CONTRATO_{quote.ExternalServiceCode}";
        }

        if (quote.Carrier == CarrierType.Fretops)
        {
            return string.IsNullOrWhiteSpace(quote.ExternalServiceCode)
                ? quote.Carrier.ToString().ToUpperInvariant()
                : quote.ExternalServiceCode;
        }

        var carrierCode = quote.Carrier.ToString().ToUpperInvariant();
        var serviceSlug = Slugify(quote.ServiceName);

        // Sem serviço, ou serviço == transportadora → só a transportadora.
        if (string.IsNullOrEmpty(serviceSlug) || serviceSlug == carrierCode)
            return carrierCode;

        // Já no formato SERVICO_TRANSPORTADORA → mantém.
        if (serviceSlug.EndsWith($"_{carrierCode}", StringComparison.Ordinal))
            return serviceSlug;

        // Remove a transportadora do slug do serviço para não repetir (ex.: LOGGI_ECONOMICO → ECONOMICO).
        if (serviceSlug.StartsWith($"{carrierCode}_", StringComparison.Ordinal))
            serviceSlug = serviceSlug[(carrierCode.Length + 1)..];

        return $"{serviceSlug}_{carrierCode}";
    }

    /// <summary>
    /// Gera um id de cotação pseudoaleatório de 10 dígitos. Usado apenas quando a
    /// transportadora não atribui o próprio id à cotação (ver
    /// <see cref="CarrierQuote.ExternalQuoteId"/> — a Fretops, por exemplo, exige que
    /// devolvamos o <c>checkout_ref</c> dela, não um id inventado aqui).
    /// </summary>
    private static long GenerateQuoteId() => Random.Shared.NextInt64(1_000_000_000, long.MaxValue);

    /// <summary>Normaliza um texto em código: MAIÚSCULAS, sem acento, não-alfanumérico vira "_".</summary>
    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (var c in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_');
        }

        return Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
    }
}

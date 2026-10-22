using Api.Domain.Carriers;
using Api.Domain.Rules;
using Api.Domain.Shipping;
using Api.Infrastructure.Carriers;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Application.Shipping;

public sealed class ShippingSimulator(
    AppDbContext db,
    RuleEngine ruleEngine,
    CarrierQuoteAggregator carriers,
    IOptions<CarrierOptions> carrierOptions,
    TimeProvider timeProvider,
    ILogger<ShippingSimulator> logger) : IShippingSimulator
{
    private readonly CarrierOptions _carrierOptions = carrierOptions.Value;

    public async Task<SimulateResponse> SimulateAsync(
        SimulateRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = BuildContext(request);

        // 1) Motor de regras (interno, sem custo de rede).
        var rules = await db.ShippingRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(cancellationToken);

        var evaluation = ruleEngine.Evaluate(rules, context);

        // 2) Transportadoras (para transparência e para os casos sem decisão de regra).
        var carrierQuotes = await QueryCarriersAsync(context, cancellationToken);

        // 3) Opções finais: decisão da regra tem precedência; senão, as cotações.
        var finalOptions = BuildFinalOptions(evaluation, carrierQuotes);

        return new SimulateResponse
        {
            DestinationZipCode = context.DestinationZipCode,
            CartTotal = context.Cart.TotalValue,
            TotalWeightKg = context.Cart.TotalWeightKg,
            RuleDecided = evaluation.HasDecision,
            DecidingRule = evaluation.DecidingRule is { } d ? ToDto(d) : null,
            AppliedRules = evaluation.AppliedRules.Select(ToDto).ToList(),
            CarrierQuotes = carrierQuotes
                .Select(q => new CarrierQuoteDto(q.Carrier.ToString(), q.ServiceName, q.Price, q.EstimatedDays))
                .ToList(),
            FinalOptions = finalOptions
        };
    }

    private ShippingContext BuildContext(SimulateRequest request)
    {
        var items = request.Items
            .Select((i, index) => new CartItem(
                i.UnitPrice,
                i.Quantity,
                i.WeightKg,
                i is { LengthCm: not null, WidthCm: not null, HeightCm: not null }
                    ? new PackageDimensions(i.LengthCm.Value, i.WidthCm.Value, i.HeightCm.Value)
                    : null,
                string.IsNullOrWhiteSpace(i.Sku) ? $"SKU-{index + 1}" : i.Sku))
            .ToList();

        var cart = new Cart(items);
        var when = request.RequestedAt ?? timeProvider.GetUtcNow();
        return new ShippingContext(request.DestinationZipCode, cart, when);
    }

    private async Task<IReadOnlyList<CarrierQuote>> QueryCarriersAsync(
        ShippingContext context, CancellationToken cancellationToken)
    {
        if (!Cep.TryNormalize(_carrierOptions.OriginZipCode, out var origin))
        {
            logger.LogWarning(
                "Carriers:OriginZipCode não configurado/ inválido; pulando cotação de transportadoras.");
            return [];
        }

        var request = new CarrierQuoteRequest
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

        return await carriers.GetQuotesAsync(request, cancellationToken);
    }

    private static IReadOnlyList<ShippingOptionDto> BuildFinalOptions(
        RuleEvaluation evaluation, IReadOnlyList<CarrierQuote> carrierQuotes)
    {
        if (evaluation.HasDecision && evaluation.DecidingRule is { } deciding)
        {
            var result = evaluation.Result;

            // Frete grátis: uma opção só, com o prazo da transportadora mais barata.
            if (result.IsFreeShipping)
            {
                var cheapest = carrierQuotes.OrderBy(q => q.Price).FirstOrDefault();
                return
                [
                    // Grátis para o cliente (price 0), com o prazo da transportadora mais barata.
                    new ShippingOptionDto(
                        Source: cheapest is not null ? cheapest.Carrier.ToString() : "rule",
                        Name: deciding.RuleName,
                        Price: 0m,
                        EstimatedDays: cheapest?.EstimatedDays,
                        IsFree: true)
                ];
            }

            return
            [
                new ShippingOptionDto(
                    Source: "rule",
                    Name: deciding.RuleName,
                    Price: result.Price ?? 0m,
                    EstimatedDays: result.EstimatedDays,
                    IsFree: false)
            ];
        }

        return carrierQuotes
            .OrderBy(q => q.Price)
            .Select(q => new ShippingOptionDto(
                Source: q.Carrier.ToString(),
                Name: q.ServiceName,
                Price: q.Price,
                EstimatedDays: q.EstimatedDays,
                IsFree: q.Price == 0m))
            .ToList();
    }

    private static AppliedRuleDto ToDto(AppliedRule r) => new(
        r.RuleId, r.RuleName, r.RuleType, r.Priority,
        r.Result.IsDecided, r.Result.IsFreeShipping, r.Result.Price);
}

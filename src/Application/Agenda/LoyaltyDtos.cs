namespace Application.Agenda;

// Planos de fidelidade (contrato JSON camelCase, ver docs/contrato-api.md do app).
// Valor cheio do mês = soma de preço do serviço × vezes por mês; o desconto % sai dele.

/// <summary>Serviço do plano e quantas vezes por mês o cliente pode fazer.</summary>
public sealed record PlanItemDto(string ServiceId, string ServiceName, int TimesPerMonth);

/// <summary>Valor do plano para um tipo de veículo. Tipo sem preço em algum serviço do plano não aparece.</summary>
public sealed record PlanPriceDto(string VehicleType, decimal FullPrice, decimal Price);

/// <summary>Plano oferecido pela filial. <c>ActiveClients</c>: clientes com este plano em andamento.</summary>
public sealed record LoyaltyPlanDto(
    string Id,
    string Name,
    string Description,
    decimal DiscountPercent,
    IReadOnlyList<PlanItemDto> Items,
    IReadOnlyList<PlanPriceDto> Prices,
    int ActiveClients);

public sealed class PlanItemRequest
{
    public string? ServiceId { get; set; }
    public int TimesPerMonth { get; set; }
}

/// <summary>Cadastro do plano da filial.</summary>
public sealed class LoyaltyPlanRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal DiscountPercent { get; set; }
    public List<PlanItemRequest>? Items { get; set; }
}

/// <summary>Mensalidade de um mês do plano: <c>Status</c> "paid" ou "pending"; paga guarda o valor e quando ('AAAA-MM-DDTHH:mm').</summary>
public sealed record PlanChargeDto(string Month, decimal Amount, string Status, string? PaidAt);

/// <summary>Uso de um serviço do plano no mês consultado.</summary>
public sealed record PlanUsageDto(string ServiceId, string ServiceName, int TimesPerMonth, int Used);

/// <summary>
/// Plano de um cliente, com o preço fechado para o carro dele. <c>Month</c> ('AAAA-MM') é o mês do <c>Usage</c>.
/// <c>EndDate</c> preenchida = encerrado (cobre os atendimentos até esse dia).
/// </summary>
public sealed record ClientPlanDto(
    string Id,
    string ClientId,
    string? PlanId,
    string Name,
    decimal DiscountPercent,
    string? VehicleId,
    string VehicleType,
    decimal FullPrice,
    decimal Price,
    string StartDate,
    string? EndDate,
    string Month,
    IReadOnlyList<PlanUsageDto> Usage,
    IReadOnlyList<PlanChargeDto> Charges,
    int UpcomingBookings);

/// <summary>
/// Adesão do cliente a um plano. Com <c>PlanId</c>, vem do modelo (pode ter sido ajustado só para ele);
/// sem, é um plano personalizado. Com <c>VehicleId</c>, o tipo de veículo sai do carro.
/// </summary>
public sealed class ClientPlanRequest
{
    public string? PlanId { get; set; }
    public string? Name { get; set; }
    public decimal DiscountPercent { get; set; }
    public List<PlanItemRequest>? Items { get; set; }
    public string? VehicleId { get; set; }
    public string? VehicleType { get; set; }
}

/// <summary>Plano que cobriria um serviço num dia: <c>Used</c> de <c>TimesPerMonth</c> no mês <c>Month</c> ('AAAA-MM').</summary>
public sealed record PlanCoverageDto(string PlanId, string PlanName, int TimesPerMonth, int Used, string Month)
{
    public bool HasRoom => Used < TimesPerMonth;
}

/// <summary>Quantos agendamentos de cada serviço o plano já cobriu no período (lido pelo repositório).</summary>
public sealed class PlanUsageRow
{
    public Guid ClientPlanId { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public int Used { get; set; }
}

/// <summary>Agendamentos marcados depois de hoje que cada plano cobre.</summary>
public sealed class PlanUpcomingRow
{
    public Guid ClientPlanId { get; set; }
    public int Bookings { get; set; }
}

/// <summary>Plano em andamento de um cliente (para a etiqueta na lista de clientes).</summary>
public sealed class ClientPlanNameRow
{
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Clientes em andamento por plano da filial.</summary>
public sealed class PlanSubscribersRow
{
    public Guid PlanId { get; set; }
    public int Clients { get; set; }
}

namespace Folha.Domain.Entities;

/// <summary>
/// Plano de parcelamento fixo amarrado a um cartão de crédito.
/// Representa o "molde" (ex.: 10x de R$200, 3 já pagas) — as parcelas do mês
/// são materializadas como transações normais conforme o cartão vira.
/// </summary>
public sealed class CreditCardInstallment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CreditCardId { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Total de parcelas (N).</summary>
    public short InstallmentTotal { get; set; }

    /// <summary>Parcelas já pagas no momento do cadastro (P). As automáticas começam em P+1.</summary>
    public short InstallmentsPaid { get; set; }

    /// <summary>Valor de cada parcela.</summary>
    public decimal InstallmentAmount { get; set; }

    /// <summary>Data da primeira parcela gerada automaticamente (parcela P+1).</summary>
    public DateTime StartDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

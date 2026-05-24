namespace Folha.Domain.Entities;

public sealed class Bill
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? AccountId { get; set; }
    public int? CategoryId { get; set; }
    public Guid? RecurrenceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Kind { get; set; } = "payable";
    /// <summary>
    /// Data de vencimento. Opcional — contas avulsas (sem boleto/data definida)
    /// podem ser cadastradas e ganhar uma data depois via update.
    /// </summary>
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Valor acumulado já pago. Quando atinge <see cref="Amount"/> a conta é
    /// promovida automaticamente para paid/received.
    /// </summary>
    public decimal PaidAmount { get; set; }

    public DateTime? PaidAt { get; set; }
    public Guid? PaidTransactionId { get; set; }

    /// <summary>Parcela atual (1..N) — opcional, informativo.</summary>
    public short? InstallmentCurrent { get; set; }

    /// <summary>Total de parcelas (≥ <see cref="InstallmentCurrent"/>) — opcional.</summary>
    public short? InstallmentTotal { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

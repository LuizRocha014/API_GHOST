namespace Folha.Application.Transactions;

public sealed record TransactionDto(
    Guid Id,
    Guid UserId,
    Guid? AccountId,
    Guid? CreditCardId,
    Guid? CreditCardStatementId,
    int CategoryId,
    Guid? BillId,
    Guid? RecurrenceId,
    Guid? ParentTransactionId,
    Guid? TransferId,
    string Description,
    string? Place,
    string? Notes,
    decimal Amount,
    string Kind,
    DateTime OccurredAt,
    short? InstallmentNumber,
    short? InstallmentTotal,
    bool IsPending,
    bool IsExcludedFromReports,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateTransactionRequest(
    Guid? AccountId,
    Guid? CreditCardId,
    Guid? CreditCardStatementId,
    int CategoryId,
    string Description,
    string? Place,
    string? Notes,
    decimal Amount,
    string Kind,
    DateTime OccurredAt,
    short? InstallmentNumber = null,
    short? InstallmentTotal = null,
    bool IsPending = false,
    // Id opcional gerado pelo cliente (offline-first).
    Guid? Id = null,
    // Quando a transação registra o pagamento de uma conta fixa, amarra ao bill.
    Guid? BillId = null,
    // Quando true, a transação aparece na timeline mas é excluída de relatórios
    // (gráficos, saldo, weekDelta). Usado p/ pagamentos de bills que já têm
    // sua linha própria em FOLHA_Bills.
    bool IsExcludedFromReports = false);

public sealed record UpdateTransactionRequest(
    Guid? AccountId,
    Guid? CreditCardId,
    Guid? CreditCardStatementId,
    int CategoryId,
    string Description,
    string? Place,
    string? Notes,
    decimal Amount,
    string Kind,
    DateTime OccurredAt,
    short? InstallmentNumber,
    short? InstallmentTotal,
    bool IsPending,
    bool IsExcludedFromReports);

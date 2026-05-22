namespace Folha.Application.Transfers;

public sealed record TransferDto(
    Guid Id,
    Guid UserId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    decimal Fee,
    DateTime OccurredAt,
    string? Description,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    decimal Fee,
    DateTime OccurredAt,
    string? Description = null,
    string? Notes = null);

public sealed record UpdateTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    decimal Fee,
    DateTime OccurredAt,
    string? Description,
    string? Notes);

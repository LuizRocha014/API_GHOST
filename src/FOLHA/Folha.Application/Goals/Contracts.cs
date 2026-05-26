namespace Folha.Application.Goals;

public sealed record GoalDto(
    Guid Id,
    Guid UserId,
    Guid? AccountId,
    string Title,
    string? Description,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateTime? TargetDate,
    string? Icon,
    string? ColorHex,
    bool IsCompleted,
    DateTime? CompletedAt,
    bool IsArchived,
    decimal? MonthlyYieldPercent,
    bool IsCdb,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateGoalRequest(
    Guid? AccountId,
    string Title,
    string? Description,
    decimal TargetAmount,
    DateTime? TargetDate,
    string? Icon,
    string? ColorHex,
    decimal? MonthlyYieldPercent = null,
    bool IsCdb = false,
    // Id opcional gerado pelo cliente (offline-first).
    Guid? Id = null);

public sealed record UpdateGoalRequest(
    Guid? AccountId,
    string Title,
    string? Description,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateTime? TargetDate,
    string? Icon,
    string? ColorHex,
    bool IsCompleted,
    bool IsArchived,
    decimal? MonthlyYieldPercent = null,
    bool IsCdb = false);

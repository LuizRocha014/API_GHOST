namespace Abasta.Application.Invitations;

public sealed record InvitationDto(
    Guid Id,
    Guid CompanyId,
    string Email,
    string Role,
    string Status,
    DateTime ExpiresAt,
    DateTime CreatedAt);

public sealed record CreateInvitationRequest(string Email, string Role = "collaborator");

/// <summary>Token em texto puro devolvido só na criação (deve ser enviado por e-mail/link).</summary>
public sealed record CreateInvitationResult(InvitationDto Invitation, string Token);

public sealed record AcceptInvitationRequest(string Token, string FullName, string Password);

public enum AcceptInvitationStatus { Success, NotFound, Expired, EmailTaken }

public sealed record AcceptInvitationResult(AcceptInvitationStatus Status, Guid? UserId, Guid? CompanyId);

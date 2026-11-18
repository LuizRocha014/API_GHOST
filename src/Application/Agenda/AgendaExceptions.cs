namespace Application.Agenda;

/// <summary>Erro de regra da agenda; a Api devolve como <c>{ "mensagem": ... }</c>.</summary>
public abstract class AgendaException(string message) : Exception(message);

/// <summary>O horário foi ocupado entre a consulta e a reserva (HTTP 409).</summary>
public sealed class AgendaConflictException(string message = "Esse horário acabou de ser ocupado. Escolha outro horário.")
    : AgendaException(message);

/// <summary>Dado inválido (HTTP 400).</summary>
public sealed class AgendaValidationException(string message) : AgendaException(message);

/// <summary>Registro não encontrado (HTTP 404).</summary>
public sealed class AgendaNotFoundException(string message = "Registro não encontrado.") : AgendaException(message);

/// <summary>Usuário sem acesso à filial ou à ação (HTTP 403).</summary>
public sealed class AgendaForbiddenException(string message = "Você não tem acesso a esta filial.") : AgendaException(message);

/// <summary>Sessão que não vale mais: usuário desativado ou apagado (HTTP 401, o app volta para o login).</summary>
public sealed class AgendaUnauthorizedException(string message = "Sua sessão expirou. Entre de novo.") : AgendaException(message);

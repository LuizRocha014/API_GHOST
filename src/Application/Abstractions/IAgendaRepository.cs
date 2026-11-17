using Application.Agenda;
using Domain.Agenda;

namespace Application.Abstractions;

/// <summary>Persistência da agenda (por filial).</summary>
public interface IAgendaRepository
{
    /* ---------- Acesso ---------- */
    Task<IReadOnlyList<AgendaBranchDto>> ListBranchesForUserAsync(Guid userId, CancellationToken ct = default);
    Task<bool> CanAccessBranchAsync(Guid userId, Guid branchId, CancellationToken ct = default);
    Task<string?> GetBranchNameAsync(Guid branchId, CancellationToken ct = default);

    /* ---------- Configuração ---------- */
    Task<string?> GetConfigJsonAsync(Guid branchId, CancellationToken ct = default);
    Task SaveConfigJsonAsync(Guid branchId, string json, CancellationToken ct = default);

    /* ---------- Leitura ---------- */
    Task<IReadOnlyList<AgendaBooking>> ListBookingsAsync(Guid branchId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<AgendaBooking?> GetBookingAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AgendaBlock>> ListBlocksAsync(Guid branchId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<AgendaReminder>> ListRemindersAsync(Guid branchId, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>Filiais com lembrete pendente vencido até <paramref name="until"/> (horário local), para o worker.</summary>
    Task<IReadOnlyList<Guid>> ListBranchesWithDueRemindersAsync(DateTime until, CancellationToken ct = default);
    Task<AgendaClient?> GetClientAsync(Guid branchId, IReadOnlyList<string> phoneVariants, CancellationToken ct = default);
    Task<IReadOnlyList<AgendaClient>> ListClientsAsync(Guid branchId, CancellationToken ct = default);

    /* ---------- Escrita simples ---------- */
    Task<AgendaBlock> AddBlockAsync(AgendaBlock block, CancellationToken ct = default);
    Task<bool> DeleteBlockAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task<bool> DeleteBookingAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task<int> PruneBookingsAsync(Guid branchId, DateTime before, CancellationToken ct = default);

    /// <summary>
    /// Abre uma transação que trava os agendamentos da filial no dia (UPDLOCK, HOLDLOCK): reservas
    /// concorrentes para o mesmo dia esperam umas pelas outras. Tudo que for gravado pelo
    /// <see cref="IAgendaDayTransaction"/> entra junto no commit.
    /// </summary>
    Task<T> InDayTransactionAsync<T>(Guid branchId, DateTime date, Func<IAgendaDayTransaction, Task<T>> work, CancellationToken ct = default);

    /// <summary>Transação simples (sem travar dia), para status, lembretes e clientes.</summary>
    Task<T> InTransactionAsync<T>(Guid branchId, Func<IAgendaDayTransaction, Task<T>> work, CancellationToken ct = default);
}

/// <summary>Operações dentro da transação da agenda.</summary>
public interface IAgendaDayTransaction
{
    /// <summary>Agendamentos ativos (não excluídos) do dia travado, de todos os boxes.</summary>
    Task<IReadOnlyList<AgendaBooking>> DayBookingsAsync(DateTime date);
    Task<IReadOnlyList<AgendaBlock>> DayBlocksAsync(DateTime date);
    Task<AgendaBooking?> GetBookingAsync(Guid id);

    Task InsertBookingAsync(AgendaBooking booking);
    Task UpdateBookingStatusAsync(Guid id, string status, DateTime updatedAtUtc);

    Task<AgendaClient?> GetClientAsync(IReadOnlyList<string> phoneVariants);
    Task UpsertClientAsync(AgendaClient client);

    Task InsertReminderAsync(AgendaReminder reminder);
    Task<IReadOnlyList<AgendaReminder>> RemindersOfBookingAsync(Guid bookingId);
    Task UpdateReminderAsync(AgendaReminder reminder);

    /// <summary>Lembretes pendentes vencidos até <paramref name="until"/> (ou os ids pedidos), travados para o envio.</summary>
    Task<IReadOnlyList<AgendaReminder>> PendingRemindersAsync(DateTime until, IReadOnlyList<Guid>? onlyIds);
    Task<AgendaReminder?> ReminderByWamIdAsync(string wamId);

    /// <summary>Cancela os lembretes pendentes de agendamentos do telefone (opt-out do cliente).</summary>
    Task CancelPendingRemindersOfPhonesAsync(IReadOnlyList<string> phoneVariants);
}

/// <summary>Envio do lembrete pelo canal (WhatsApp Cloud API). Devolve o id da mensagem.</summary>
public interface IReminderSender
{
    Task<string> SendAsync(AgendaBooking booking, AgendaConfigModel config, CancellationToken ct = default);
}

/// <summary>Relógio no fuso da estética (as datas da agenda são locais).</summary>
public interface IAgendaClock
{
    DateTime NowLocal { get; }
}

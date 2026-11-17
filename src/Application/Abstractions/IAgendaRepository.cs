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
    Task<AgendaClient?> GetClientByIdAsync(Guid branchId, Guid id, CancellationToken ct = default);

    /// <summary>Carros ativos dos clientes da filial (de um cliente só, com <paramref name="clientId"/>).</summary>
    Task<IReadOnlyList<AgendaClientVehicle>> ListClientVehiclesAsync(Guid branchId, Guid? clientId, CancellationToken ct = default);

    /// <summary>Totais dos agendamentos por cliente; <paramref name="today"/> separa o que já passou do que vem.</summary>
    Task<IReadOnlyList<ClientStatsRow>> ClientStatsAsync(Guid branchId, DateTime today, Guid? clientId, CancellationToken ct = default);

    /// <summary>Agendamentos do cliente, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<AgendaBooking>> ListClientBookingsAsync(Guid branchId, Guid clientId, int limit, CancellationToken ct = default);

    /* ---------- Planos de fidelidade ---------- */
    Task<IReadOnlyList<AgendaLoyaltyPlan>> ListLoyaltyPlansAsync(Guid branchId, CancellationToken ct = default);
    Task<AgendaLoyaltyPlan?> GetLoyaltyPlanAsync(Guid branchId, Guid id, CancellationToken ct = default);
    Task SaveLoyaltyPlanAsync(AgendaLoyaltyPlan plan, CancellationToken ct = default);
    Task<bool> DeactivateLoyaltyPlanAsync(Guid branchId, Guid id, CancellationToken ct = default);

    /// <summary>Clientes com plano em andamento (sem encerramento), por modelo de plano.</summary>
    Task<IReadOnlyList<PlanSubscribersRow>> PlanSubscribersAsync(Guid branchId, CancellationToken ct = default);

    /// <summary>Planos do cliente (em andamento e encerrados).</summary>
    Task<IReadOnlyList<AgendaClientPlan>> ListClientPlansAsync(Guid branchId, Guid clientId, CancellationToken ct = default);

    /// <summary>Nomes dos planos em andamento por cliente (de um cliente só, com <paramref name="clientId"/>).</summary>
    Task<IReadOnlyList<ClientPlanNameRow>> ClientPlanNamesAsync(Guid branchId, Guid? clientId, CancellationToken ct = default);

    /// <summary>Agendamentos ativos cobertos por cada plano no período, por serviço.</summary>
    Task<IReadOnlyList<PlanUsageRow>> PlanUsageAsync(Guid branchId, IReadOnlyList<Guid> clientPlanIds, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>Planos dos clientes da filial, em andamento e encerrados (de um cliente só, com <paramref name="clientId"/>).</summary>
    Task<IReadOnlyList<AgendaClientPlan>> ListBranchClientPlansAsync(Guid branchId, Guid? clientId, CancellationToken ct = default);

    /// <summary>Mensalidades pagas dos clientes da filial (de um cliente só, com <paramref name="clientId"/>).</summary>
    Task<IReadOnlyList<AgendaPlanPayment>> ListPlanPaymentsAsync(Guid branchId, Guid? clientId, CancellationToken ct = default);

    /// <summary>Quantos agendamentos marcados depois de <paramref name="today"/> cada plano cobre.</summary>
    Task<IReadOnlyList<PlanUpcomingRow>> PlanUpcomingAsync(Guid branchId, IReadOnlyList<Guid> clientPlanIds, DateTime today, CancellationToken ct = default);

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
    Task<AgendaClient?> GetClientByIdAsync(Guid id);

    /// <summary>Grava nome, telefone, consentimento, VIP e observações (cria se não existir).</summary>
    Task UpsertClientAsync(AgendaClient client);
    Task DeactivateClientAsync(Guid id, DateTime updatedAtUtc);

    Task<IReadOnlyList<AgendaClientVehicle>> ClientVehiclesAsync(Guid clientId);
    Task UpsertVehicleAsync(AgendaClientVehicle vehicle);
    Task DeactivateVehiclesAsync(Guid clientId, IReadOnlyList<Guid> keepIds, DateTime updatedAtUtc);

    /// <summary>Planos do cliente (em andamento e encerrados), travados para a transação.</summary>
    Task<IReadOnlyList<AgendaClientPlan>> ClientPlansAsync(Guid clientId);
    Task UpsertClientPlanAsync(AgendaClientPlan plan);

    /// <summary>Trava o uso do plano no mês até o fim da transação: duas reservas não levam a última vez do mês.</summary>
    Task LockPlanMonthAsync(Guid clientPlanId, DateTime date);

    /// <summary>Agendamentos ativos do serviço que o plano já cobriu entre as datas.</summary>
    Task<int> PlanServiceUsageAsync(Guid clientPlanId, string serviceId, DateTime from, DateTime to);

    /// <summary>Agendamentos marcados que o plano cobre depois de <paramref name="date"/>.</summary>
    Task<IReadOnlyList<AgendaBooking>> PlanBookingsAfterAsync(Guid clientPlanId, DateTime date);

    /// <summary>Tira o agendamento do plano e grava o preço normal do serviço.</summary>
    Task ReleaseBookingFromPlanAsync(Guid bookingId, decimal? price, DateTime updatedAtUtc);

    Task<IReadOnlyList<AgendaPlanPayment>> PlanPaymentsAsync(Guid clientPlanId);
    Task InsertPlanPaymentAsync(AgendaPlanPayment payment);
    Task DeactivatePlanPaymentAsync(Guid id, DateTime updatedAtUtc);

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

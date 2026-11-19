using System.Collections.Concurrent;

namespace Application.Agenda;

/// <summary>O que mudou na filial desde <see cref="Version"/> (vazio = nada no tempo de espera).</summary>
public sealed record AgendaChangesDto(long Version, IReadOnlyList<string> Kinds);

/// <summary>
/// Avisos de mudança por filial, para os aparelhos atualizarem na hora em vez de consultar a cada 15 s: quem grava publica
/// (o app pela API, o bot, o worker do lembrete) e o app espera em <c>GET mudancas?desde=</c> (long-poll). Em memória: vale
/// para uma instância da API; com várias, cada aparelho ainda recebe o que passou pela instância dele e o resto no ciclo
/// de segurança do app.
/// </summary>
public sealed class AgendaChangeFeed
{
    public const string Config = "config";
    public const string Bookings = "bookings";
    public const string Blocks = "blocks";
    public const string Reminders = "reminders";
    public const string Clients = "clients";
    public const string Plans = "plans";
    public const string Inbox = "inbox";
    public const string Parking = "parking";
    public static readonly string[] All = [Config, Bookings, Blocks, Reminders, Clients, Plans, Inbox, Parking];

    private const int KeepLast = 200;

    /// <summary>Começa no relógio: depois de reiniciar a API a versão é sempre maior que a de antes.</summary>
    private readonly long _base = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private readonly ConcurrentDictionary<Guid, BranchFeed> _branches = new();

    private sealed class BranchFeed(long start)
    {
        public readonly object Gate = new();
        public long Version = start;
        public readonly Queue<(long Version, string[] Kinds)> Log = new();
        public TaskCompletionSource Next = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private BranchFeed Feed(Guid branchId) => _branches.GetOrAdd(branchId, _ => new BranchFeed(_base));

    public void Publish(Guid branchId, params string[] kinds)
    {
        if (kinds.Length == 0) return;
        var feed = Feed(branchId);
        TaskCompletionSource fire;
        lock (feed.Gate)
        {
            feed.Version++;
            feed.Log.Enqueue((feed.Version, kinds));
            while (feed.Log.Count > KeepLast) feed.Log.Dequeue();
            fire = feed.Next;
            feed.Next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        fire.TrySetResult();
    }

    /// <summary>
    /// O que mudou depois de <paramref name="since"/>. Se nada mudou, espera até <paramref name="wait"/> por uma mudança.
    /// Versão desconhecida (de antes de reiniciar, ou mais antiga que o histórico) devolve tudo.
    /// </summary>
    public async Task<AgendaChangesDto> WaitAsync(Guid branchId, long since, TimeSpan wait, CancellationToken ct)
    {
        var feed = Feed(branchId);
        Task next;
        lock (feed.Gate)
        {
            if (Collect(feed, since) is { } now) return now;
            next = feed.Next.Task;
        }
        if (wait > TimeSpan.Zero)
        {
            try
            {
                await next.WaitAsync(wait, ct);
            }
            catch (TimeoutException)
            {
            }
        }
        lock (feed.Gate)
        {
            return Collect(feed, since) ?? new AgendaChangesDto(feed.Version, []);
        }
    }

    private static AgendaChangesDto? Collect(BranchFeed feed, long since)
    {
        if (since < 0 || since == feed.Version) return since < 0 ? new AgendaChangesDto(feed.Version, []) : null;
        var oldest = feed.Log.Count > 0 ? feed.Log.Peek().Version : feed.Version + 1;
        if (since > feed.Version || since < oldest - 1) return new AgendaChangesDto(feed.Version, All);
        var kinds = feed.Log.Where(e => e.Version > since).SelectMany(e => e.Kinds).Distinct().ToList();
        return kinds.Count == 0 ? null : new AgendaChangesDto(feed.Version, kinds);
    }
}

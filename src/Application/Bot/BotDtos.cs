namespace Application.Bot;

/// <summary>
/// Mensagem simulada do cliente para o bot (POST /api/agenda/{branchId}/bot/mensagens): passa pelo mesmo
/// fluxo do webhook, mas as respostas voltam no corpo em vez de irem para o WhatsApp.
/// </summary>
public sealed class BotSimulationRequest
{
    public string Telefone { get; set; } = string.Empty;

    /// <summary>Texto digitado.</summary>
    public string? Texto { get; set; }

    /// <summary>Id da opção tocada (lista, botão ou botão do template, ex.: conf:{id}).</summary>
    public string? OpcaoId { get; set; }
    public string? OpcaoTitulo { get; set; }

    /// <summary>Nome do perfil no WhatsApp.</summary>
    public string? Nome { get; set; }

    /// <summary>Id da mensagem; repetir o mesmo id simula o reenvio do webhook (o bot ignora).</summary>
    public string? Id { get; set; }

    /// <summary>text, interactive, button, audio, image… Sem valor: interactive quando há opção, senão text.</summary>
    public string? Tipo { get; set; }
}

public sealed record BotOptionDto(string Id, string Titulo, string? Descricao);

public sealed record BotMessageDto(
    string Tipo,
    string Texto,
    string? Botao,
    IReadOnlyList<BotOptionDto> Opcoes,
    string? Template,
    IReadOnlyList<string>? Parametros);

public sealed record BotSimulationResponse(string Estado, IReadOnlyList<BotMessageDto> Mensagens);

/// <summary>
/// Mensagem da conversa. <c>Direcao</c>: in (cliente), out (bot) ou agent (equipe, com <c>Autor</c>).
/// Lista, botões e template trazem o botão e as opções que o cliente viu.
/// </summary>
public sealed record BotLogDto(
    string Telefone,
    string Direcao,
    string Tipo,
    string Texto,
    string? WamId,
    string Em,
    string? Autor = null,
    string? Botao = null,
    IReadOnlyList<BotOptionDto>? Opcoes = null);

/// <summary>
/// Conversa com um número. <c>JanelaAberta</c>: o cliente escreveu nas últimas 24 h, então a equipe pode responder
/// com texto livre (fora dela, a Meta só aceita template).
/// </summary>
public sealed record BotConversationDto(
    string Estado,
    IReadOnlyList<BotLogDto> Mensagens,
    string? Telefone = null,
    string? Nome = null,
    string? ClienteId = null,
    bool JanelaAberta = false,
    string? JanelaFechaEm = null);

/// <summary>Linha da caixa de entrada.</summary>
public sealed record BotConversationSummaryDto(
    string Telefone,
    string Nome,
    string? ClienteId,
    string Estado,
    bool PrecisaAtendente,
    int NaoLidas,
    bool JanelaAberta,
    BotLogDto Ultima);

public sealed class BotReplyRequest
{
    public string Texto { get; set; } = string.Empty;
}

/// <summary>O que cadastrar no WhatsApp Manager para o lembrete D-1.</summary>
public sealed record ReminderTemplateDto(
    string Nome,
    string Idioma,
    string Categoria,
    string Texto,
    IReadOnlyList<string> Variaveis,
    IReadOnlyList<string> Botoes,
    string Previa);

public static class BotDtoMapping
{
    public static BotMessageDto ToDto(this OutgoingMessage m) => m switch
    {
        ListMessage l => new BotMessageDto(l.Kind, l.Text, l.Button, l.Rows.Select(Option).ToList(), null, null),
        ButtonsMessage b => new BotMessageDto(b.Kind, b.Text, null, b.Buttons.Select(Option).ToList(), null, null),
        TemplateMessage t => new BotMessageDto(
            t.Kind, t.Preview, null, t.Buttons.Select(x => new BotOptionDto(x.Payload, x.Title, null)).ToList(), t.Name, t.BodyParams),
        _ => new BotMessageDto(m.Kind, m.Body, null, [], null, null),
    };

    private static BotOptionDto Option(BotOption o) => new(o.Id, o.Title, o.Description);

    private sealed record MetaModel(string? Botao, IReadOnlyList<BotOptionDto> Opcoes);

    /// <summary>Botão e opções da mensagem, para guardar junto da conversa. Texto simples não tem.</summary>
    public static string? MetaJson(OutgoingMessage m)
    {
        var dto = m.ToDto();
        return dto.Opcoes.Count == 0 && dto.Botao is null
            ? null
            : System.Text.Json.JsonSerializer.Serialize(new MetaModel(dto.Botao, dto.Opcoes), Agenda.AgendaConfigModel.Json);
    }

    public static BotLogDto ToDto(this BotLogEntry e)
    {
        MetaModel? meta = null;
        if (!string.IsNullOrEmpty(e.Meta))
        {
            try
            {
                meta = System.Text.Json.JsonSerializer.Deserialize<MetaModel>(e.Meta, Agenda.AgendaConfigModel.Json);
            }
            catch (System.Text.Json.JsonException)
            {
            }
        }
        return new BotLogDto(
            e.Phone, e.Direction, e.Kind, e.Body, e.WamId,
            DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            e.Author, meta?.Botao, meta?.Opcoes);
    }
}

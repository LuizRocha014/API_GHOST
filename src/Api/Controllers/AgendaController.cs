using System.Text.Json;
using Api.Agenda;
using Application.Agenda;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Agenda da estética, por filial: configuração, agendamentos, bloqueios, lembretes e clientes.
/// Contrato em docs/contrato-api.md do app. Erros de regra voltam como <c>{ "mensagem": "..." }</c>.
/// </summary>
[ApiController]
[Route("api/agenda")]
[Authorize]
[TypeFilter(typeof(AgendaExceptionFilter))]
[TypeFilter(typeof(AgendaBranchAccessFilter))]
public sealed class AgendaController(IAgendaService agenda) : ControllerBase
{
    /* ---------- Filiais ---------- */

    /// <summary>Filiais que o usuário pode abrir na agenda (sem vínculo cadastrado = todas).</summary>
    [HttpGet("filiais")]
    [ProducesResponseType(typeof(IReadOnlyList<AgendaBranchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgendaBranchDto>>> Branches(CancellationToken ct) =>
        Ok(await agenda.BranchesAsync(AgendaBranchAccessFilter.UserId(User), ct));

    /* ---------- Configuração ---------- */

    /// <summary>Configuração da agenda da filial (cria a padrão no primeiro acesso).</summary>
    [HttpGet("{branchId:guid}/config")]
    [Produces("application/json")]
    public async Task<IActionResult> GetConfig(Guid branchId, CancellationToken ct) =>
        Content(await agenda.GetConfigAsync(branchId, ct), "application/json");

    /// <summary>Grava a configuração inteira (boxes, serviços, horários e personalizações).</summary>
    [HttpPut("{branchId:guid}/config")]
    [Produces("application/json")]
    public async Task<IActionResult> SaveConfig(Guid branchId, [FromBody] JsonElement config, CancellationToken ct) =>
        Content(await agenda.SaveConfigAsync(branchId, config.GetRawText(), ct), "application/json");

    /* ---------- Agendamentos ---------- */

    /// <summary>Agendamentos dos dias entre <paramref name="de"/> e <paramref name="ate"/> (AAAA-MM-DD).</summary>
    [HttpGet("{branchId:guid}/agendamentos")]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> ListBookings(
        Guid branchId, [FromQuery] string de, [FromQuery] string ate, CancellationToken ct) =>
        Ok(await agenda.ListBookingsAsync(branchId, de, ate, ct));

    /// <summary>Um agendamento.</summary>
    [HttpGet("{branchId:guid}/agendamentos/{id:guid}")]
    public async Task<ActionResult<BookingDto>> GetBooking(Guid branchId, Guid id, CancellationToken ct) =>
        Ok(await agenda.GetBookingAsync(branchId, id, ct));

    /// <summary>Horários livres para o serviço e tipo de veículo no dia.</summary>
    [HttpGet("{branchId:guid}/disponibilidade")]
    public async Task<ActionResult<IReadOnlyList<SlotOptionDto>>> Availability(
        Guid branchId,
        [FromQuery] string data,
        [FromQuery] string servicoId,
        [FromQuery] string tipoVeiculo,
        [FromQuery] Guid? ignorarAgendamentoId,
        [FromQuery] int antecedenciaMin,
        CancellationToken ct) =>
        Ok(await agenda.AvailabilityAsync(branchId, data, servicoId, tipoVeiculo, ignorarAgendamentoId, antecedenciaMin, ct));

    /// <summary>Reserva um horário (409 se foi ocupado nesse meio tempo).</summary>
    [HttpPost("{branchId:guid}/agendamentos")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> CreateBooking(Guid branchId, [FromBody] NewBookingRequest request, CancellationToken ct)
    {
        var created = await agenda.CreateBookingAsync(branchId, request, ct);
        return CreatedAtAction(nameof(GetBooking), new { branchId, id = created.Id }, created);
    }

    /// <summary>Muda o status (em atendimento, concluído, cancelado, não compareceu).</summary>
    [HttpPatch("{branchId:guid}/agendamentos/{id:guid}/status")]
    public async Task<ActionResult<BookingDto>> SetStatus(Guid branchId, Guid id, [FromBody] StatusRequest request, CancellationToken ct) =>
        Ok(await agenda.SetStatusAsync(branchId, id, request.Status, ct));

    /// <summary>Remarca: reserva o novo horário e cancela o antigo na mesma transação.</summary>
    [HttpPost("{branchId:guid}/agendamentos/{id:guid}/remarcar")]
    public async Task<ActionResult<BookingDto>> Reschedule(Guid branchId, Guid id, [FromBody] RescheduleRequest request, CancellationToken ct) =>
        Ok(await agenda.RescheduleAsync(branchId, id, request, ct));

    /// <summary>Exclui um agendamento e os lembretes dele.</summary>
    [HttpDelete("{branchId:guid}/agendamentos/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteBooking(Guid branchId, Guid id, CancellationToken ct)
    {
        await agenda.DeleteBookingAsync(branchId, id, ct);
        return NoContent();
    }

    /// <summary>Apaga de vez os agendamentos (e lembretes) anteriores a <paramref name="antesDe"/> (LGPD).</summary>
    [HttpDelete("{branchId:guid}/agendamentos")]
    public async Task<ActionResult<PruneResult>> Prune(Guid branchId, [FromQuery] string antesDe, CancellationToken ct) =>
        Ok(new PruneResult(await agenda.PruneAsync(branchId, antesDe, ct)));

    /// <summary>Registra a resposta do cliente ao lembrete (confirmou, quer remarcar, cancelou).</summary>
    [HttpPost("{branchId:guid}/agendamentos/{id:guid}/resposta")]
    public async Task<IActionResult> Answer(Guid branchId, Guid id, [FromBody] AnswerRequest request, CancellationToken ct)
    {
        var reminder = await agenda.RegisterAnswerAsync(branchId, id, request, ct);
        return reminder is null ? NoContent() : Ok(reminder);
    }

    /* ---------- Bloqueios ---------- */

    /// <summary>Bloqueios dos dias no período.</summary>
    [HttpGet("{branchId:guid}/bloqueios")]
    public async Task<ActionResult<IReadOnlyList<BlockDto>>> ListBlocks(
        Guid branchId, [FromQuery] string de, [FromQuery] string ate, CancellationToken ct) =>
        Ok(await agenda.ListBlocksAsync(branchId, de, ate, ct));

    /// <summary>Bloqueia um intervalo (boxId nulo = todos os boxes).</summary>
    [HttpPost("{branchId:guid}/bloqueios")]
    public async Task<ActionResult<BlockDto>> CreateBlock(Guid branchId, [FromBody] NewBlockRequest request, CancellationToken ct) =>
        Ok(await agenda.CreateBlockAsync(branchId, request, ct));

    /// <summary>Remove um bloqueio.</summary>
    [HttpDelete("{branchId:guid}/bloqueios/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteBlock(Guid branchId, Guid id, CancellationToken ct)
    {
        await agenda.DeleteBlockAsync(branchId, id, ct);
        return NoContent();
    }

    /* ---------- Lembretes ---------- */

    /// <summary>Lembretes dos agendamentos no período.</summary>
    [HttpGet("{branchId:guid}/lembretes")]
    public async Task<ActionResult<IReadOnlyList<ReminderDto>>> ListReminders(
        Guid branchId, [FromQuery] string de, [FromQuery] string ate, CancellationToken ct) =>
        Ok(await agenda.ListRemindersAsync(branchId, de, ate, ct));

    /// <summary>Gatilho manual do worker: envia os lembretes vencidos (ou os ids pedidos).</summary>
    [HttpPost("{branchId:guid}/lembretes/processar")]
    public async Task<ActionResult<IReadOnlyList<ReminderDispatchDto>>> Dispatch(
        Guid branchId, [FromBody] DispatchRequest request, CancellationToken ct) =>
        Ok(await agenda.DispatchRemindersAsync(branchId, request, ct));

    /// <summary>Status de entrega da mensagem (em produção vem do webhook da Meta).</summary>
    [HttpPost("{branchId:guid}/lembretes/entrega")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delivery(Guid branchId, [FromBody] DeliveryRequest request, CancellationToken ct)
    {
        await agenda.MarkDeliveryAsync(branchId, request, ct);
        return NoContent();
    }

    /* ---------- Clientes ---------- */

    /// <summary>Clientes da filial, com os carros e o histórico (atendimentos, faltas, total gasto).</summary>
    [HttpGet("{branchId:guid}/clientes")]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> ListClients(Guid branchId, CancellationToken ct) =>
        Ok(await agenda.ListClientsAsync(branchId, ct));

    /// <summary>Cadastra o cliente com os carros. 409 se o WhatsApp já é de outro cliente.</summary>
    [HttpPost("{branchId:guid}/clientes")]
    public async Task<ActionResult<ClientDto>> CreateClient(Guid branchId, [FromBody] ClientFormRequest request, CancellationToken ct)
    {
        var created = await agenda.CreateClientAsync(branchId, request, ct);
        return CreatedAtAction(nameof(GetClientById), new { branchId, id = created.Id }, created);
    }

    [HttpGet("{branchId:guid}/clientes/{id:guid}")]
    public async Task<ActionResult<ClientDto>> GetClientById(Guid branchId, Guid id, CancellationToken ct) =>
        Ok(await agenda.GetClientByIdAsync(branchId, id, ct));

    /// <summary>Atualiza o cadastro. A lista de carros vem inteira: carro que não vier sai do cliente.</summary>
    [HttpPut("{branchId:guid}/clientes/{id:guid}")]
    public async Task<ActionResult<ClientDto>> UpdateClient(Guid branchId, Guid id, [FromBody] ClientFormRequest request, CancellationToken ct) =>
        Ok(await agenda.UpdateClientAsync(branchId, id, request, ct));

    /// <summary>Tira o cliente do cadastro. Os agendamentos dele continuam na agenda.</summary>
    [HttpDelete("{branchId:guid}/clientes/{id:guid}")]
    public async Task<IActionResult> DeleteClient(Guid branchId, Guid id, CancellationToken ct)
    {
        await agenda.DeleteClientAsync(branchId, id, ct);
        return NoContent();
    }

    /// <summary>Agendamentos do cliente, do mais novo para o mais antigo.</summary>
    [HttpGet("{branchId:guid}/clientes/{id:guid}/agendamentos")]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> ClientBookings(Guid branchId, Guid id, CancellationToken ct) =>
        Ok(await agenda.ClientBookingsAsync(branchId, id, ct));

    /* ---------- Planos de fidelidade ---------- */

    /// <summary>Planos da filial com o valor por tipo de veículo e quantos clientes estão em cada um.</summary>
    [HttpGet("{branchId:guid}/planos")]
    public async Task<ActionResult<IReadOnlyList<LoyaltyPlanDto>>> ListPlans(Guid branchId, CancellationToken ct) =>
        Ok(await agenda.ListPlansAsync(branchId, ct));

    /// <summary>Cadastra um plano: serviços com vezes por mês e desconto em % sobre o valor cheio.</summary>
    [HttpPost("{branchId:guid}/planos")]
    [ProducesResponseType(typeof(LoyaltyPlanDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<LoyaltyPlanDto>> CreatePlan(Guid branchId, [FromBody] LoyaltyPlanRequest request, CancellationToken ct)
    {
        var created = await agenda.CreatePlanAsync(branchId, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Altera o plano. Quem já aderiu continua com o plano como estava.</summary>
    [HttpPut("{branchId:guid}/planos/{id:guid}")]
    public async Task<ActionResult<LoyaltyPlanDto>> UpdatePlan(Guid branchId, Guid id, [FromBody] LoyaltyPlanRequest request, CancellationToken ct) =>
        Ok(await agenda.UpdatePlanAsync(branchId, id, request, ct));

    /// <summary>Tira o plano da lista. Os clientes que aderiram continuam com ele até encerrar.</summary>
    [HttpDelete("{branchId:guid}/planos/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePlan(Guid branchId, Guid id, CancellationToken ct)
    {
        await agenda.DeletePlanAsync(branchId, id, ct);
        return NoContent();
    }

    /// <summary>Planos do cliente com o uso de cada serviço no mês de <paramref name="data"/> (AAAA-MM-DD; vazio = mês atual).</summary>
    [HttpGet("{branchId:guid}/clientes/{id:guid}/planos")]
    public async Task<ActionResult<IReadOnlyList<ClientPlanDto>>> ClientPlans(Guid branchId, Guid id, [FromQuery] string? data, CancellationToken ct) =>
        Ok(await agenda.ListClientPlansAsync(branchId, id, data, ct));

    /// <summary>Coloca o cliente num plano (do modelo ou personalizado). 409 se o carro já tem plano em andamento.</summary>
    [HttpPost("{branchId:guid}/clientes/{id:guid}/planos")]
    [ProducesResponseType(typeof(ClientPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientPlanDto>> CreateClientPlan(Guid branchId, Guid id, [FromBody] ClientPlanRequest request, CancellationToken ct)
    {
        var created = await agenda.CreateClientPlanAsync(branchId, id, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Altera o plano do cliente (serviços, vezes, desconto, carro). O preço é recalculado.</summary>
    [HttpPut("{branchId:guid}/clientes/{id:guid}/planos/{planoId:guid}")]
    public async Task<ActionResult<ClientPlanDto>> UpdateClientPlan(
        Guid branchId, Guid id, Guid planoId, [FromBody] ClientPlanRequest request, CancellationToken ct) =>
        Ok(await agenda.UpdateClientPlanAsync(branchId, id, planoId, request, ct));

    /// <summary>Encerra o plano do cliente hoje (os atendimentos já marcados continuam como estão).</summary>
    [HttpPost("{branchId:guid}/clientes/{id:guid}/planos/{planoId:guid}/encerrar")]
    public async Task<ActionResult<ClientPlanDto>> EndClientPlan(Guid branchId, Guid id, Guid planoId, CancellationToken ct) =>
        Ok(await agenda.EndClientPlanAsync(branchId, id, planoId, ct));

    /// <summary>Marca a mensalidade do mês (AAAA-MM) como paga, com o valor atual do plano.</summary>
    [HttpPost("{branchId:guid}/clientes/{id:guid}/planos/{planoId:guid}/mensalidades/{mes}/pagar")]
    public async Task<ActionResult<ClientPlanDto>> PayCharge(Guid branchId, Guid id, Guid planoId, string mes, CancellationToken ct) =>
        Ok(await agenda.PayChargeAsync(branchId, id, planoId, mes, ct));

    /// <summary>Desfaz o pagamento da mensalidade do mês (volta a pendente).</summary>
    [HttpDelete("{branchId:guid}/clientes/{id:guid}/planos/{planoId:guid}/mensalidades/{mes}")]
    public async Task<ActionResult<ClientPlanDto>> UndoCharge(Guid branchId, Guid id, Guid planoId, string mes, CancellationToken ct) =>
        Ok(await agenda.UndoChargeAsync(branchId, id, planoId, mes, ct));

    /// <summary>Cliente pelo telefone (aceita com e sem o nono dígito).</summary>
    [HttpGet("{branchId:guid}/clientes/{telefone}")]
    public async Task<ActionResult<ClientDto>> GetClient(Guid branchId, string telefone, CancellationToken ct)
    {
        var client = await agenda.GetClientAsync(branchId, telefone, ct);
        return client is null ? NotFound(new { mensagem = "Cliente não encontrado." }) : Ok(client);
    }

    /// <summary>Cria ou atualiza o cliente e o consentimento do lembrete.</summary>
    [HttpPut("{branchId:guid}/clientes/{telefone}")]
    public async Task<ActionResult<ClientDto>> UpsertClient(Guid branchId, string telefone, [FromBody] ClientRequest request, CancellationToken ct) =>
        Ok(await agenda.UpsertClientAsync(branchId, telefone, request, ct));
}

namespace Application.Agenda;

/// <summary>
/// Quem está mexendo na agenda nesta requisição (vai para o histórico): a pessoa logada, o "Bot do WhatsApp" (worker do
/// webhook) ou o "Sistema" (lembretes, tarefas). Um por escopo.
/// </summary>
public sealed class AgendaActor
{
    public const string Bot = "Bot do WhatsApp";
    public const string System = "Sistema";

    public string Name { get; set; } = System;
    public Guid? UserId { get; set; }
}

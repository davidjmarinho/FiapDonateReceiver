namespace FiapDonateReceiver.Worker.Events;

public record DoacaoRecebidaEvent(
    Guid DoacaoId,
    Guid IdCampanha,
    decimal ValorDoacao,
    DateTimeOffset DataHoraRecebida);

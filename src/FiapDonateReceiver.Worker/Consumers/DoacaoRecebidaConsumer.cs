using FiapDonateReceiver.Infrastructure;
using FiapDonateReceiver.Worker.Events;
using MassTransit;

namespace FiapDonateReceiver.Worker.Consumers;

public class DoacaoRecebidaConsumer : IConsumer<DoacaoRecebidaEvent>
{
    private readonly DoacaoRepository _repositorio;
    private readonly ILogger<DoacaoRecebidaConsumer> _logger;

    public DoacaoRecebidaConsumer(DoacaoRepository repositorio, ILogger<DoacaoRecebidaConsumer> logger)
    {
        _repositorio = repositorio;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DoacaoRecebidaEvent> context)
    {
        var evento = context.Message;

        var processada = await _repositorio.ProcessarDoacaoAsync(
            evento.DoacaoId,
            evento.IdCampanha,
            evento.ValorDoacao,
            evento.DataHoraRecebida,
            context.CancellationToken);

        if (!processada)
        {
            _logger.LogInformation(
                "Doacao {DoacaoId} ja havia sido processada anteriormente, ignorando duplicata.",
                evento.DoacaoId);
        }
    }
}

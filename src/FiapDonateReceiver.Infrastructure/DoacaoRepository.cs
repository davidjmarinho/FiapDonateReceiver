using FiapDonateReceiver.Domain;
using Microsoft.EntityFrameworkCore;

namespace FiapDonateReceiver.Infrastructure;

public class DoacaoRepository
{
    private readonly ReceiverDbContext _dbContext;

    public DoacaoRepository(ReceiverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ProcessarDoacaoAsync(
        Guid doacaoId,
        Guid idCampanha,
        decimal valorDoacao,
        DateTimeOffset dataHoraRecebida,
        CancellationToken cancellationToken = default)
    {
        var jaProcessada = await _dbContext.Doacoes
            .AnyAsync(d => d.Id == doacaoId, cancellationToken);

        if (jaProcessada)
        {
            return false;
        }

        var campanha = await _dbContext.Campanhas
            .SingleOrDefaultAsync(c => c.Id == idCampanha, cancellationToken);

        var doacao = new Doacao
        {
            Id = doacaoId,
            IdCampanha = idCampanha,
            ValorDoacao = valorDoacao,
            DataHoraRecebida = dataHoraRecebida,
            DataHoraProcessada = DateTimeOffset.UtcNow
        };

        DoacaoProcessor.Processar(campanha, doacao);

        _dbContext.Doacoes.Add(doacao);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}

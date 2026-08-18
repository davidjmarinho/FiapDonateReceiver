using FiapDonateReceiver.Domain;
using Microsoft.EntityFrameworkCore;

namespace FiapDonateReceiver.Infrastructure;

public class ReceiverDbContext : DbContext
{
    public ReceiverDbContext(DbContextOptions<ReceiverDbContext> options) : base(options)
    {
    }

    public DbSet<Campanha> Campanhas => Set<Campanha>();
    public DbSet<Doacao> Doacoes => Set<Doacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Campanha>(entity =>
        {
            // Campanhas é criada pelas migrations do repositório da API; aqui mapeamos
            // apenas as colunas necessárias e excluímos a tabela das migrations deste projeto.
            entity.ToTable("Campanhas", t => t.ExcludeFromMigrations());
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Status).HasConversion<string>();
        });

        modelBuilder.Entity<Doacao>(entity =>
        {
            entity.ToTable("Doacoes");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Status).HasConversion<string>();
        });
    }
}

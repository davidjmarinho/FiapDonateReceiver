using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FiapDonateReceiver.Infrastructure;

public class ReceiverDbContextFactory : IDesignTimeDbContextFactory<ReceiverDbContext>
{
    public ReceiverDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RECEIVER_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=conexao_solidaria;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ReceiverDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ReceiverDbContext(optionsBuilder.Options);
    }
}

# FiapDonateReceiver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the FiapDonateReceiver Worker — the donation-processing microservice for the "Conexão Solidária" hackathon platform — that consumes `DoacaoRecebidaEvent` messages from RabbitMQ and updates the donated campaign's total, idempotently.

**Architecture:** A single ASP.NET Core Minimal API host (`FiapDonateReceiver.Worker`) runs a MassTransit/RabbitMQ consumer in the background and exposes `/health` and `/metrics` over HTTP. Business rules live in a dependency-free `FiapDonateReceiver.Domain` project; PostgreSQL persistence lives in `FiapDonateReceiver.Infrastructure`, which maps the shared `Campanhas` table (owned by the separate API repository) and owns a new `Doacoes` table.

**Tech Stack:** .NET 8 (LTS), MassTransit 9.2.0 + RabbitMQ.Client 7.2.1, EF Core 8 + Npgsql, PostgreSQL, RabbitMQ, prometheus-net, xUnit, Docker, Kubernetes, GitHub Actions.

## Global Constraints

- Target framework: **.NET 8** (LTS) for every project in the solution.
- Database: **PostgreSQL**, shared with the separate API repository — this repo does **not** own the `Campanhas` table schema (it maps a read/write subset of columns and excludes it from its own migrations).
- Messaging: **RabbitMQ**, consumed via **MassTransit**. Queue name: `doacao-recebida-queue`.
- Business rule: a donation must **not** be credited to a campaign with `Status` `Cancelada` or `Concluida`, or to a campaign that cannot be found — it must instead be recorded as rejected.
- Idempotency: redelivery of the same `DoacaoId` must **not** credit the campaign twice.
- CI must build the code and produce a Docker image on every push to `main` (deploy to Kubernetes is optional and out of scope for CI).
- Kubernetes manifests must include Deployment, Service, and ConfigMap.
- The application must expose a health/metrics endpoint (`/health` and/or `/metrics`).
- Unit tests (xUnit) must cover the domain's business rules.
- All package versions specified in this plan were verified to compile together against .NET 8 in a throwaway probe project — use the exact versions given unless a step tells you otherwise.

---

### Task 1: Solution and project scaffolding

**Files:**
- Create: `FiapDonateReceiver.slnx`
- Create: `src/FiapDonateReceiver.Domain/FiapDonateReceiver.Domain.csproj`
- Create: `src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj`
- Create: `src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj`
- Create: `tests/FiapDonateReceiver.Domain.Tests/FiapDonateReceiver.Domain.Tests.csproj`
- Create: `tests/FiapDonateReceiver.Infrastructure.Tests/FiapDonateReceiver.Infrastructure.Tests.csproj`
- Delete: `src/FiapDonateReceiver.Domain/Class1.cs`, `src/FiapDonateReceiver.Infrastructure/Class1.cs`, `tests/FiapDonateReceiver.Domain.Tests/UnitTest1.cs`, `tests/FiapDonateReceiver.Infrastructure.Tests/UnitTest1.cs` (template placeholders)

**Interfaces:**
- Produces: solution file `FiapDonateReceiver.slnx` referencing all 5 projects; project-reference graph: `Infrastructure` → `Domain`; `Worker` → `Domain`, `Infrastructure`; `Domain.Tests` → `Domain`; `Infrastructure.Tests` → `Infrastructure`.

All commands below run from the repository root (`FiapDonateReceiver/`, the directory containing `.git`).

- [ ] **Step 1: Create the solution file**

Run: `dotnet new sln -n FiapDonateReceiver`
Expected: creates `FiapDonateReceiver.slnx` in the repo root (the `dotnet new sln` default format is `.slnx`).

- [ ] **Step 2: Scaffold the five projects**

```bash
dotnet new classlib -n FiapDonateReceiver.Domain -o src/FiapDonateReceiver.Domain -f net8.0
dotnet new classlib -n FiapDonateReceiver.Infrastructure -o src/FiapDonateReceiver.Infrastructure -f net8.0
dotnet new web -n FiapDonateReceiver.Worker -o src/FiapDonateReceiver.Worker -f net8.0
dotnet new xunit -n FiapDonateReceiver.Domain.Tests -o tests/FiapDonateReceiver.Domain.Tests -f net8.0
dotnet new xunit -n FiapDonateReceiver.Infrastructure.Tests -o tests/FiapDonateReceiver.Infrastructure.Tests -f net8.0
```

- [ ] **Step 3: Remove template placeholder files**

```bash
rm src/FiapDonateReceiver.Domain/Class1.cs
rm src/FiapDonateReceiver.Infrastructure/Class1.cs
rm tests/FiapDonateReceiver.Domain.Tests/UnitTest1.cs
rm tests/FiapDonateReceiver.Infrastructure.Tests/UnitTest1.cs
```

- [ ] **Step 4: Add all projects to the solution**

```bash
dotnet sln FiapDonateReceiver.slnx add src/FiapDonateReceiver.Domain/FiapDonateReceiver.Domain.csproj src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj tests/FiapDonateReceiver.Domain.Tests/FiapDonateReceiver.Domain.Tests.csproj tests/FiapDonateReceiver.Infrastructure.Tests/FiapDonateReceiver.Infrastructure.Tests.csproj
```

- [ ] **Step 5: Wire project references**

```bash
dotnet add src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj reference src/FiapDonateReceiver.Domain/FiapDonateReceiver.Domain.csproj
dotnet add src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj reference src/FiapDonateReceiver.Domain/FiapDonateReceiver.Domain.csproj
dotnet add src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj reference src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj
dotnet add tests/FiapDonateReceiver.Domain.Tests/FiapDonateReceiver.Domain.Tests.csproj reference src/FiapDonateReceiver.Domain/FiapDonateReceiver.Domain.csproj
dotnet add tests/FiapDonateReceiver.Infrastructure.Tests/FiapDonateReceiver.Infrastructure.Tests.csproj reference src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj
```

- [ ] **Step 6: Verify the solution builds**

Run: `dotnet build FiapDonateReceiver.slnx`
Expected: `Compilação com êxito.` (Build succeeded), `0 Erro(s)`.

- [ ] **Step 7: Commit**

```bash
git add FiapDonateReceiver.slnx src tests
git commit -m "chore: scaffold solution and project structure"
```

---

### Task 2: Domain — entities and donation-processing rule (TDD)

**Files:**
- Create: `src/FiapDonateReceiver.Domain/CampanhaStatus.cs`
- Create: `src/FiapDonateReceiver.Domain/Campanha.cs`
- Create: `src/FiapDonateReceiver.Domain/DoacaoStatus.cs`
- Create: `src/FiapDonateReceiver.Domain/Doacao.cs`
- Create: `src/FiapDonateReceiver.Domain/DoacaoProcessor.cs`
- Test: `tests/FiapDonateReceiver.Domain.Tests/DoacaoProcessorTests.cs`

**Interfaces:**
- Produces: `Campanha { Guid Id; CampanhaStatus Status; decimal ValorArrecadado; }`, `Doacao { Guid Id; Guid IdCampanha; decimal ValorDoacao; DateTimeOffset DataHoraRecebida; DateTimeOffset DataHoraProcessada; DoacaoStatus Status; }`, `DoacaoProcessor.Processar(Campanha? campanha, Doacao doacao): void` — mutates `doacao.Status` and, when credited, increments `campanha.ValorArrecadado`. Used by Task 3's `DoacaoRepository`.

- [ ] **Step 1: Create the entity and enum types**

`src/FiapDonateReceiver.Domain/CampanhaStatus.cs`:

```csharp
namespace FiapDonateReceiver.Domain;

public enum CampanhaStatus
{
    Ativa,
    Concluida,
    Cancelada
}
```

`src/FiapDonateReceiver.Domain/Campanha.cs`:

```csharp
namespace FiapDonateReceiver.Domain;

public class Campanha
{
    public Guid Id { get; set; }
    public CampanhaStatus Status { get; set; }
    public decimal ValorArrecadado { get; set; }
}
```

`src/FiapDonateReceiver.Domain/DoacaoStatus.cs`:

```csharp
namespace FiapDonateReceiver.Domain;

public enum DoacaoStatus
{
    Creditada,
    Rejeitada
}
```

`src/FiapDonateReceiver.Domain/Doacao.cs`:

```csharp
namespace FiapDonateReceiver.Domain;

public class Doacao
{
    public Guid Id { get; set; }
    public Guid IdCampanha { get; set; }
    public decimal ValorDoacao { get; set; }
    public DateTimeOffset DataHoraRecebida { get; set; }
    public DateTimeOffset DataHoraProcessada { get; set; }
    public DoacaoStatus Status { get; set; }
}
```

- [ ] **Step 2: Write the failing tests for `DoacaoProcessor`**

`tests/FiapDonateReceiver.Domain.Tests/DoacaoProcessorTests.cs`:

```csharp
using FiapDonateReceiver.Domain;

namespace FiapDonateReceiver.Domain.Tests;

public class DoacaoProcessorTests
{
    [Fact]
    public void Processar_CampanhaAtiva_CreditaDoacaoEAtualizaValorArrecadado()
    {
        var campanha = new Campanha
        {
            Id = Guid.NewGuid(),
            Status = CampanhaStatus.Ativa,
            ValorArrecadado = 100m
        };
        var doacao = new Doacao
        {
            Id = Guid.NewGuid(),
            IdCampanha = campanha.Id,
            ValorDoacao = 50m
        };

        DoacaoProcessor.Processar(campanha, doacao);

        Assert.Equal(DoacaoStatus.Creditada, doacao.Status);
        Assert.Equal(150m, campanha.ValorArrecadado);
    }

    [Theory]
    [InlineData(CampanhaStatus.Cancelada)]
    [InlineData(CampanhaStatus.Concluida)]
    public void Processar_CampanhaNaoAtiva_RejeitaDoacaoENaoAlteraValorArrecadado(CampanhaStatus status)
    {
        var campanha = new Campanha
        {
            Id = Guid.NewGuid(),
            Status = status,
            ValorArrecadado = 100m
        };
        var doacao = new Doacao
        {
            Id = Guid.NewGuid(),
            IdCampanha = campanha.Id,
            ValorDoacao = 50m
        };

        DoacaoProcessor.Processar(campanha, doacao);

        Assert.Equal(DoacaoStatus.Rejeitada, doacao.Status);
        Assert.Equal(100m, campanha.ValorArrecadado);
    }

    [Fact]
    public void Processar_CampanhaInexistente_RejeitaDoacao()
    {
        var doacao = new Doacao
        {
            Id = Guid.NewGuid(),
            IdCampanha = Guid.NewGuid(),
            ValorDoacao = 50m
        };

        DoacaoProcessor.Processar(null, doacao);

        Assert.Equal(DoacaoStatus.Rejeitada, doacao.Status);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/FiapDonateReceiver.Domain.Tests/FiapDonateReceiver.Domain.Tests.csproj`
Expected: build FAILS with `CS0103: The name 'DoacaoProcessor' does not exist in the current context` (the type doesn't exist yet).

- [ ] **Step 4: Implement `DoacaoProcessor`**

`src/FiapDonateReceiver.Domain/DoacaoProcessor.cs`:

```csharp
namespace FiapDonateReceiver.Domain;

public static class DoacaoProcessor
{
    public static void Processar(Campanha? campanha, Doacao doacao)
    {
        doacao.Status = AvaliarStatus(campanha);

        if (doacao.Status == DoacaoStatus.Creditada && campanha is not null)
        {
            campanha.ValorArrecadado += doacao.ValorDoacao;
        }
    }

    private static DoacaoStatus AvaliarStatus(Campanha? campanha)
    {
        if (campanha is null)
        {
            return DoacaoStatus.Rejeitada;
        }

        return campanha.Status == CampanhaStatus.Ativa
            ? DoacaoStatus.Creditada
            : DoacaoStatus.Rejeitada;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/FiapDonateReceiver.Domain.Tests/FiapDonateReceiver.Domain.Tests.csproj`
Expected: `Aprovado! - Com falha: 0, Aprovado: 4` (Passed! - Failed: 0, Passed: 4) — 4 tests: the two `[Theory]` cases count individually.

- [ ] **Step 6: Commit**

```bash
git add src/FiapDonateReceiver.Domain tests/FiapDonateReceiver.Domain.Tests
git commit -m "feat: add domain entities and donation-processing rule"
```

---

### Task 3: Infrastructure — DbContext and idempotent repository (TDD via EF Core InMemory)

**Files:**
- Modify: `src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj`
- Modify: `tests/FiapDonateReceiver.Infrastructure.Tests/FiapDonateReceiver.Infrastructure.Tests.csproj`
- Create: `src/FiapDonateReceiver.Infrastructure/ReceiverDbContext.cs`
- Create: `src/FiapDonateReceiver.Infrastructure/DoacaoRepository.cs`
- Test: `tests/FiapDonateReceiver.Infrastructure.Tests/DoacaoRepositoryTests.cs`

**Interfaces:**
- Consumes: `FiapDonateReceiver.Domain.Campanha`, `Doacao`, `DoacaoProcessor.Processar` (Task 2).
- Produces: `ReceiverDbContext(DbContextOptions<ReceiverDbContext>)` with `DbSet<Campanha> Campanhas`, `DbSet<Doacao> Doacoes`; `DoacaoRepository(ReceiverDbContext).ProcessarDoacaoAsync(Guid doacaoId, Guid idCampanha, decimal valorDoacao, DateTimeOffset dataHoraRecebida, CancellationToken = default): Task<bool>` — returns `false` when the `doacaoId` was already processed (no-op), `true` otherwise. Used by Task 5's consumer.

- [ ] **Step 1: Add EF Core packages**

```bash
dotnet add src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.11
dotnet add src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.11
dotnet add tests/FiapDonateReceiver.Infrastructure.Tests/FiapDonateReceiver.Infrastructure.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory --version 8.0.11
```

Note: `Npgsql.EntityFrameworkCore.PostgreSQL` versions ≥ 9 only target `net10.0` — the `--version 8.0.11` pin above is required for this to resolve against `net8.0`. Do not omit it.

- [ ] **Step 2: Create `ReceiverDbContext`**

`src/FiapDonateReceiver.Infrastructure/ReceiverDbContext.cs`:

```csharp
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
```

- [ ] **Step 3: Write the failing tests for `DoacaoRepository`**

`tests/FiapDonateReceiver.Infrastructure.Tests/DoacaoRepositoryTests.cs`:

```csharp
using FiapDonateReceiver.Domain;
using Microsoft.EntityFrameworkCore;

namespace FiapDonateReceiver.Infrastructure.Tests;

public class DoacaoRepositoryTests
{
    private static ReceiverDbContext CriarContexto(string nomeBanco)
    {
        var options = new DbContextOptionsBuilder<ReceiverDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;
        return new ReceiverDbContext(options);
    }

    [Fact]
    public async Task ProcessarDoacaoAsync_CampanhaAtiva_CreditaValorEPersisteDoacao()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var campanhaId = Guid.NewGuid();

        await using (var contexto = CriarContexto(nomeBanco))
        {
            contexto.Campanhas.Add(new Campanha { Id = campanhaId, Status = CampanhaStatus.Ativa, ValorArrecadado = 0m });
            await contexto.SaveChangesAsync();
        }

        bool processada;
        await using (var contexto = CriarContexto(nomeBanco))
        {
            var repositorio = new DoacaoRepository(contexto);
            processada = await repositorio.ProcessarDoacaoAsync(
                Guid.NewGuid(), campanhaId, 75m, DateTimeOffset.UtcNow);
        }

        await using (var contexto = CriarContexto(nomeBanco))
        {
            Assert.True(processada);
            var campanha = await contexto.Campanhas.SingleAsync(c => c.Id == campanhaId);
            Assert.Equal(75m, campanha.ValorArrecadado);
            var doacao = await contexto.Doacoes.SingleAsync();
            Assert.Equal(DoacaoStatus.Creditada, doacao.Status);
        }
    }

    [Fact]
    public async Task ProcessarDoacaoAsync_DoacaoJaProcessada_NaoCreditaNovamente()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var campanhaId = Guid.NewGuid();
        var doacaoId = Guid.NewGuid();

        await using (var contexto = CriarContexto(nomeBanco))
        {
            contexto.Campanhas.Add(new Campanha { Id = campanhaId, Status = CampanhaStatus.Ativa, ValorArrecadado = 0m });
            await contexto.SaveChangesAsync();
        }

        await using (var contexto = CriarContexto(nomeBanco))
        {
            var repositorio = new DoacaoRepository(contexto);
            await repositorio.ProcessarDoacaoAsync(doacaoId, campanhaId, 75m, DateTimeOffset.UtcNow);
        }

        bool processadaDeNovo;
        await using (var contexto = CriarContexto(nomeBanco))
        {
            var repositorio = new DoacaoRepository(contexto);
            processadaDeNovo = await repositorio.ProcessarDoacaoAsync(doacaoId, campanhaId, 75m, DateTimeOffset.UtcNow);
        }

        await using (var contexto = CriarContexto(nomeBanco))
        {
            Assert.False(processadaDeNovo);
            var campanha = await contexto.Campanhas.SingleAsync(c => c.Id == campanhaId);
            Assert.Equal(75m, campanha.ValorArrecadado);
        }
    }

    [Fact]
    public async Task ProcessarDoacaoAsync_CampanhaInexistente_RegistraDoacaoRejeitada()
    {
        var nomeBanco = Guid.NewGuid().ToString();

        await using var contexto = CriarContexto(nomeBanco);
        var repositorio = new DoacaoRepository(contexto);

        var processada = await repositorio.ProcessarDoacaoAsync(
            Guid.NewGuid(), Guid.NewGuid(), 75m, DateTimeOffset.UtcNow);

        Assert.True(processada);
        var doacao = await contexto.Doacoes.SingleAsync();
        Assert.Equal(DoacaoStatus.Rejeitada, doacao.Status);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test tests/FiapDonateReceiver.Infrastructure.Tests/FiapDonateReceiver.Infrastructure.Tests.csproj`
Expected: build FAILS with `CS0246: The type or namespace name 'DoacaoRepository' could not be found`.

- [ ] **Step 5: Implement `DoacaoRepository`**

`src/FiapDonateReceiver.Infrastructure/DoacaoRepository.cs`:

```csharp
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
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/FiapDonateReceiver.Infrastructure.Tests/FiapDonateReceiver.Infrastructure.Tests.csproj`
Expected: `Aprovado! - Com falha: 0, Aprovado: 3`.

- [ ] **Step 7: Commit**

```bash
git add src/FiapDonateReceiver.Infrastructure tests/FiapDonateReceiver.Infrastructure.Tests
git commit -m "feat: add ReceiverDbContext and idempotent DoacaoRepository"
```

---

### Task 4: Infrastructure — PostgreSQL EF Core migrations

**Files:**
- Create: `.config/dotnet-tools.json`
- Create: `src/FiapDonateReceiver.Infrastructure/ReceiverDbContextFactory.cs`
- Create: `src/FiapDonateReceiver.Infrastructure/Migrations/*` (generated by the `dotnet ef` tool)

**Interfaces:**
- Consumes: `ReceiverDbContext` (Task 3).
- Produces: a `Migrations/*_InitialCreate.cs` that creates only the `Doacoes` table (the `Campanhas` mapping is excluded from migrations, see Task 3 Step 2).

- [ ] **Step 1: Add a local tool manifest and install `dotnet-ef`**

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 8.0.11
```

Expected: creates `.config/dotnet-tools.json` listing `dotnet-ef` at version `8.0.11`.

- [ ] **Step 2: Create the design-time factory**

`src/FiapDonateReceiver.Infrastructure/ReceiverDbContextFactory.cs`:

```csharp
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
```

This factory is only used by the `dotnet ef` CLI tool to build the model at design time — it does not need a live database connection for `migrations add`.

- [ ] **Step 3: Generate the initial migration**

```bash
dotnet ef migrations add InitialCreate --project src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj --startup-project src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj -o Migrations
```

Expected: creates `src/FiapDonateReceiver.Infrastructure/Migrations/<timestamp>_InitialCreate.cs`, `InitialCreate.Designer.cs`, and `ReceiverDbContextModelSnapshot.cs`. Open the generated `_InitialCreate.cs` and confirm the `Up()` method only calls `migrationBuilder.CreateTable(name: "Doacoes", ...)` — it must **not** create a `Campanhas` table (that would conflict with the API repository's own migrations against the same database).

- [ ] **Step 4: Verify the solution still builds**

Run: `dotnet build FiapDonateReceiver.slnx`
Expected: `Compilação com êxito.`, `0 Erro(s)`.

- [ ] **Step 5: Commit**

```bash
git add .config src/FiapDonateReceiver.Infrastructure/ReceiverDbContextFactory.cs src/FiapDonateReceiver.Infrastructure/Migrations
git commit -m "feat: add PostgreSQL migration for the Doacoes table"
```

---

### Task 5: Worker — event contract, MassTransit consumer, and host wiring

**Files:**
- Modify: `src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj`
- Create: `src/FiapDonateReceiver.Worker/Events/DoacaoRecebidaEvent.cs`
- Create: `src/FiapDonateReceiver.Worker/Consumers/DoacaoRecebidaConsumer.cs`
- Modify: `src/FiapDonateReceiver.Worker/Program.cs` (full replace)
- Modify: `src/FiapDonateReceiver.Worker/appsettings.json` (full replace)
- Modify: `src/FiapDonateReceiver.Worker/appsettings.Development.json` (full replace)

**Interfaces:**
- Consumes: `FiapDonateReceiver.Infrastructure.ReceiverDbContext`, `DoacaoRepository.ProcessarDoacaoAsync(...)` (Task 3).
- Produces: `DoacaoRecebidaEvent(Guid DoacaoId, Guid IdCampanha, decimal ValorDoacao, DateTimeOffset DataHoraRecebida)` — the wire contract consumed from `doacao-recebida-queue`. This is the record Task 6 extends with metrics, and the payload the manual e2e test (Task 10 / README) publishes by hand.

- [ ] **Step 1: Add MassTransit packages**

```bash
dotnet add src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj package MassTransit --version 9.2.0
dotnet add src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj package MassTransit.RabbitMQ --version 9.2.0
```

- [ ] **Step 2: Define the event contract**

`src/FiapDonateReceiver.Worker/Events/DoacaoRecebidaEvent.cs`:

```csharp
namespace FiapDonateReceiver.Worker.Events;

public record DoacaoRecebidaEvent(
    Guid DoacaoId,
    Guid IdCampanha,
    decimal ValorDoacao,
    DateTimeOffset DataHoraRecebida);
```

- [ ] **Step 3: Implement the consumer**

`src/FiapDonateReceiver.Worker/Consumers/DoacaoRecebidaConsumer.cs`:

```csharp
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
```

- [ ] **Step 4: Wire the host**

`src/FiapDonateReceiver.Worker/appsettings.json` (full replace):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "ReceiverDb": "Host=localhost;Port=5432;Database=conexao_solidaria;Username=postgres;Password=postgres"
  },
  "RabbitMq": {
    "Host": "localhost",
    "VirtualHost": "/",
    "Username": "guest",
    "Password": "guest"
  }
}
```

`src/FiapDonateReceiver.Worker/appsettings.Development.json` (full replace):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

`src/FiapDonateReceiver.Worker/Program.cs` (full replace):

```csharp
using FiapDonateReceiver.Infrastructure;
using FiapDonateReceiver.Worker.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ReceiverDb")
    ?? throw new InvalidOperationException("ConnectionStrings:ReceiverDb nao configurada.");

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitVirtualHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
var rabbitUsername = builder.Configuration["RabbitMq:Username"] ?? "guest";
var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

builder.Services.AddDbContext<ReceiverDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<DoacaoRepository>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DoacaoRecebidaConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, rabbitVirtualHost, h =>
        {
            h.Username(rabbitUsername);
            h.Password(rabbitPassword);
        });

        cfg.ReceiveEndpoint("doacao-recebida-queue", e =>
        {
            e.ConfigureConsumer<DoacaoRecebidaConsumer>(context);
            e.UseMessageRetry(r => r.Immediate(3));
        });
    });
});

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "FiapDonateReceiver.Worker", status = "running" }));

app.Run();
```

- [ ] **Step 5: Verify the solution builds**

Run: `dotnet build FiapDonateReceiver.slnx`
Expected: `Compilação com êxito.`, `0 Erro(s)`. (This task has no new domain logic to unit test — the consumer's end-to-end behavior is verified manually against real RabbitMQ/PostgreSQL in Task 10.)

- [ ] **Step 6: Commit**

```bash
git add src/FiapDonateReceiver.Worker
git commit -m "feat: wire MassTransit consumer for DoacaoRecebidaEvent"
```

---

### Task 6: Worker — health checks and Prometheus metrics

**Files:**
- Modify: `src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj`
- Modify: `src/FiapDonateReceiver.Worker/Program.cs` (full replace)
- Modify: `src/FiapDonateReceiver.Worker/Consumers/DoacaoRecebidaConsumer.cs` (full replace)

**Interfaces:**
- Produces: `GET /health` (aggregate status of PostgreSQL + RabbitMQ connectivity), `GET /metrics` (Prometheus exposition format, including custom counter `receiver_doacoes_processadas_total{resultado="processada|duplicada"}`).

- [ ] **Step 1: Add health-check and metrics packages**

```bash
dotnet add src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj package AspNetCore.HealthChecks.NpgSql --version 9.0.0
dotnet add src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj package AspNetCore.HealthChecks.Rabbitmq --version 9.0.0
dotnet add src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj package prometheus-net.AspNetCore --version 8.2.1
```

Note: `AspNetCore.HealthChecks.Rabbitmq` 9.x requires an injected `IConnection` (RabbitMQ.Client 7's async API) rather than a connection string — this was verified against the installed package version. Do not use the older `AddRabbitMQ(connectionString: ...)` overload; it no longer exists in this version.

- [ ] **Step 2: Add the processed-donations counter to the consumer**

`src/FiapDonateReceiver.Worker/Consumers/DoacaoRecebidaConsumer.cs` (full replace):

```csharp
using FiapDonateReceiver.Infrastructure;
using FiapDonateReceiver.Worker.Events;
using MassTransit;
using Prometheus;

namespace FiapDonateReceiver.Worker.Consumers;

public class DoacaoRecebidaConsumer : IConsumer<DoacaoRecebidaEvent>
{
    private static readonly Counter DoacoesProcessadas = Metrics.CreateCounter(
        "receiver_doacoes_processadas_total",
        "Quantidade de doacoes processadas pelo worker, particionadas por resultado.",
        new CounterConfiguration { LabelNames = new[] { "resultado" } });

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
            DoacoesProcessadas.WithLabels("duplicada").Inc();
            return;
        }

        DoacoesProcessadas.WithLabels("processada").Inc();
    }
}
```

- [ ] **Step 3: Register health checks and metrics middleware**

`src/FiapDonateReceiver.Worker/Program.cs` (full replace):

```csharp
using FiapDonateReceiver.Infrastructure;
using FiapDonateReceiver.Worker.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ReceiverDb")
    ?? throw new InvalidOperationException("ConnectionStrings:ReceiverDb nao configurada.");

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitVirtualHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
var rabbitUsername = builder.Configuration["RabbitMq:Username"] ?? "guest";
var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";
var rabbitUri = $"amqp://{rabbitUsername}:{rabbitPassword}@{rabbitHost}{rabbitVirtualHost}";

builder.Services.AddDbContext<ReceiverDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<DoacaoRepository>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DoacaoRecebidaConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, rabbitVirtualHost, h =>
        {
            h.Username(rabbitUsername);
            h.Password(rabbitPassword);
        });

        cfg.ReceiveEndpoint("doacao-recebida-queue", e =>
        {
            e.ConfigureConsumer<DoacaoRecebidaConsumer>(context);
            e.UseMessageRetry(r => r.Immediate(3));
        });
    });
});

// Conexao dedicada ao health check (a conexao interna do MassTransit nao e
// exposta via DI), conforme recomendado pelo README do AspNetCore.HealthChecks.Rabbitmq.
builder.Services.AddSingleton<IConnection>(sp =>
{
    var factory = new ConnectionFactory { Uri = new Uri(rabbitUri) };
    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
});

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql")
    .AddRabbitMQ(name: "rabbitmq");

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "FiapDonateReceiver.Worker", status = "running" }));
app.MapHealthChecks("/health");
app.UseHttpMetrics();
app.MapMetrics();

app.Run();
```

- [ ] **Step 4: Verify the solution builds**

Run: `dotnet build FiapDonateReceiver.slnx`
Expected: `Compilação com êxito.`, `0 Erro(s)`. (Runtime verification of `/health` and `/metrics` against real infrastructure happens in Task 10, once `docker-compose` is up.)

- [ ] **Step 5: Commit**

```bash
git add src/FiapDonateReceiver.Worker
git commit -m "feat: expose /health and /metrics endpoints"
```

---

### Task 7: Docker — Dockerfile and local-development compose file

**Files:**
- Create: `Dockerfile`
- Create: `.dockerignore`
- Create: `docker-compose.yml`

**Interfaces:**
- Produces: image `fiapdonatereceiver-worker:local` listening on container port `8080`; `docker-compose.yml` services `postgres` (port 5432, db `conexao_solidaria`, user/password `postgres`/`postgres`) and `rabbitmq` (ports 5672/15672, user/password `guest`/`guest`) — matches the defaults in `appsettings.json` (Task 5).

- [ ] **Step 1: Create `.dockerignore`**

`.dockerignore`:

```
**/bin/
**/obj/
**/.vs/
**/.git/
docs/
```

- [ ] **Step 2: Create the Dockerfile**

`Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["FiapDonateReceiver.slnx", "./"]
COPY ["src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj", "src/FiapDonateReceiver.Worker/"]
COPY ["src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj", "src/FiapDonateReceiver.Infrastructure/"]
COPY ["src/FiapDonateReceiver.Domain/FiapDonateReceiver.Domain.csproj", "src/FiapDonateReceiver.Domain/"]
RUN dotnet restore "src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj"

COPY src/ src/
RUN dotnet publish "src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FiapDonateReceiver.Worker.dll"]
```

- [ ] **Step 3: Create the local development compose file**

`docker-compose.yml`:

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: conexao_solidaria
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data

  rabbitmq:
    image: rabbitmq:3.13-management
    ports:
      - "5672:5672"
      - "15672:15672"

volumes:
  postgres-data:
```

- [ ] **Step 4: Verify the image builds**

Run: `docker build -t fiapdonatereceiver-worker:local .`
Expected: build completes with `writing image sha256:...` / `naming to docker.io/library/fiapdonatereceiver-worker:local` and no errors. If Docker is not installed or not running in this environment, note that explicitly instead of claiming success.

- [ ] **Step 5: Verify the compose stack starts**

```bash
docker compose up -d
docker compose ps
```

Expected: both `postgres` and `rabbitmq` show state `running`/`healthy`. Then stop it: `docker compose down`.

- [ ] **Step 6: Commit**

```bash
git add Dockerfile .dockerignore docker-compose.yml
git commit -m "chore: add Dockerfile and local-development compose file"
```

---

### Task 8: Kubernetes manifests

**Files:**
- Create: `k8s/configmap.yaml`
- Create: `k8s/secret.example.yaml`
- Create: `k8s/deployment.yaml`
- Create: `k8s/service.yaml`
- Modify: `.gitignore` (ignore the real `k8s/secret.yaml` if a teammate creates one from the example)

**Interfaces:**
- Produces: Deployment `fiapdonatereceiver-worker` (label `app: fiapdonatereceiver-worker`, container port `8080`, probes on `/health`) wired to ConfigMap `fiapdonatereceiver-worker-config` and Secret `fiapdonatereceiver-worker-secret`; Service `fiapdonatereceiver-worker` (ClusterIP, port `8080`).

- [ ] **Step 1: Create the ConfigMap**

`k8s/configmap.yaml`:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: fiapdonatereceiver-worker-config
data:
  RabbitMq__Host: "rabbitmq"
  RabbitMq__VirtualHost: "/"
  RabbitMq__Username: "guest"
```

- [ ] **Step 2: Create the Secret example**

`k8s/secret.example.yaml`:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: fiapdonatereceiver-worker-secret
type: Opaque
stringData:
  RabbitMq__Password: "guest"
  ConnectionStrings__ReceiverDb: "Host=postgres;Port=5432;Database=conexao_solidaria;Username=postgres;Password=postgres"
```

- [ ] **Step 3: Create the Deployment**

`k8s/deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: fiapdonatereceiver-worker
spec:
  replicas: 1
  selector:
    matchLabels:
      app: fiapdonatereceiver-worker
  template:
    metadata:
      labels:
        app: fiapdonatereceiver-worker
    spec:
      containers:
        - name: worker
          image: fiapdonatereceiver-worker:local
          imagePullPolicy: IfNotPresent
          ports:
            - containerPort: 8080
          envFrom:
            - configMapRef:
                name: fiapdonatereceiver-worker-config
            - secretRef:
                name: fiapdonatereceiver-worker-secret
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 15
            periodSeconds: 15
          readinessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
```

- [ ] **Step 4: Create the Service**

`k8s/service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: fiapdonatereceiver-worker
spec:
  selector:
    app: fiapdonatereceiver-worker
  ports:
    - port: 8080
      targetPort: 8080
  type: ClusterIP
```

- [ ] **Step 5: Ignore the real secret file**

Append to `.gitignore`:

```
k8s/secret.yaml
```

- [ ] **Step 6: Validate the manifests**

Run: `kubectl apply --dry-run=client -f k8s/configmap.yaml -f k8s/secret.example.yaml -f k8s/deployment.yaml -f k8s/service.yaml`
Expected: `configmap/fiapdonatereceiver-worker-config created (dry run)`, similarly for the other 3 resources, no errors. If `kubectl` is not installed/configured in this environment, note that explicitly instead of claiming success — the YAML has still been hand-verified against the Kubernetes API shapes above.

- [ ] **Step 7: Commit**

```bash
git add k8s .gitignore
git commit -m "chore: add Kubernetes manifests for the worker"
```

---

### Task 9: CI/CD — GitHub Actions pipeline

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Produces: a workflow named `CI` triggered on push/PR to `main`, running restore → build → test → `docker build`.

- [ ] **Step 1: Create the workflow**

`.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"

      - name: Restore
        run: dotnet restore FiapDonateReceiver.slnx

      - name: Build
        run: dotnet build FiapDonateReceiver.slnx --no-restore --configuration Release

      - name: Test
        run: dotnet test FiapDonateReceiver.slnx --no-build --configuration Release

      - name: Build Docker image
        run: docker build -t fiapdonatereceiver-worker:${{ github.sha }} -f Dockerfile .
```

- [ ] **Step 2: Validate the YAML locally**

Run: `python -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml'))" ` (or any available YAML linter)
Expected: no parse errors.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add build, test, and Docker image pipeline"
```

- [ ] **Step 4: Push and verify on GitHub (confirm with the user first)**

This is the only step in this plan that touches the shared remote. Ask the user before running `git push`, since it triggers a real GitHub Actions run visible to the whole team. Once pushed, open the repository's Actions tab and confirm the `CI` workflow completes with a green checkmark.

---

### Task 10: README and end-to-end verification

**Files:**
- Create: `README.md`

**Interfaces:**
- N/A — this task documents how to exercise everything built in Tasks 1–9 end to end.

- [ ] **Step 1: Write the README**

`README.md`:

```markdown
# FiapDonateReceiver

Worker/Consumer de doações da plataforma "Conexão Solidária" (Hackathon FIAP).
Consome o evento `DoacaoRecebidaEvent` de uma fila RabbitMQ e atualiza o valor
arrecadado da campanha correspondente em PostgreSQL, de forma idempotente.

Este repositório cobre **apenas** este microsserviço. A API de
Campanhas/Usuários/Autenticação (que publica o evento) vive em outro
repositório do time.

## Arquitetura

Veja `docs/superpowers/specs/2026-08-17-fiapdonatereceiver-design.md` para o
desenho completo (decisões de arquitetura, modelo de dados, contrato do
evento e regras de negócio).

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (ou
  equivalente, com suporte a `docker compose`)
- (Opcional, para deploy local em Kubernetes) `kubectl` + um cluster local
  (Minikube, Kind ou Docker Desktop Kubernetes)

## Subindo a infraestrutura localmente

1. Suba PostgreSQL e RabbitMQ:

   ```bash
   docker compose up -d
   ```

2. Restaure as ferramentas locais (inclui o `dotnet-ef`):

   ```bash
   dotnet tool restore
   ```

3. Aplique as migrations do banco (cria a tabela `Doacoes`):

   ```bash
   dotnet ef database update --project src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj --startup-project src/FiapDonateReceiver.Infrastructure/FiapDonateReceiver.Infrastructure.csproj
   ```

   > A tabela `Campanhas` não é criada por este comando — ela pertence ao
   > repositório da API. Para testar este Worker isoladamente (sem a API no
   > ar), crie manualmente uma linha de teste, por exemplo via `psql`:
   >
   > ```sql
   > CREATE TABLE IF NOT EXISTS "Campanhas" (
   >   "Id" uuid PRIMARY KEY,
   >   "Status" text NOT NULL,
   >   "ValorArrecadado" numeric NOT NULL
   > );
   > INSERT INTO "Campanhas" ("Id", "Status", "ValorArrecadado")
   > VALUES ('11111111-1111-1111-1111-111111111111', 'Ativa', 0);
   > ```

4. Rode o Worker:

   ```bash
   dotnet run --project src/FiapDonateReceiver.Worker/FiapDonateReceiver.Worker.csproj
   ```

5. Confirme que o serviço está saudável:

   ```bash
   curl http://localhost:8080/health
   curl http://localhost:8080/metrics
   ```

## Testando o fluxo fim a fim (sem depender da API estar no ar)

1. Abra a interface de management do RabbitMQ em
   [http://localhost:15672](http://localhost:15672) (usuário/senha: `guest`/`guest`).
2. Vá em **Queues** → `doacao-recebida-queue` → **Publish message**.
3. Publique uma mensagem com o header `content_type: application/vnd.masstransit+json`
   e o seguinte corpo (ajuste `idCampanha` para o `Id` de uma campanha ativa
   existente no banco):

   ```json
   {
     "message": {
       "doacaoId": "22222222-2222-2222-2222-222222222222",
       "idCampanha": "11111111-1111-1111-1111-111111111111",
       "valorDoacao": 50.00,
       "dataHoraRecebida": "2026-08-17T12:00:00Z"
     },
     "messageType": ["urn:message:FiapDonateReceiver.Worker.Events:DoacaoRecebidaEvent"]
   }
   ```

4. Confirme no PostgreSQL que o valor foi creditado:

   ```bash
   docker compose exec postgres psql -U postgres -d conexao_solidaria -c "SELECT * FROM \"Campanhas\"; SELECT * FROM \"Doacoes\";"
   ```

   O `ValorArrecadado` da campanha deve ter subido em 50.00, e deve existir
   uma linha em `Doacoes` com `Status = Creditada`.

## Rodando os testes automatizados

```bash
dotnet test FiapDonateReceiver.slnx
```

## Deploy local em Kubernetes

```bash
docker build -t fiapdonatereceiver-worker:local .
cp k8s/secret.example.yaml k8s/secret.yaml   # ajuste credenciais se necessário
kubectl apply -f k8s/configmap.yaml -f k8s/secret.yaml -f k8s/deployment.yaml -f k8s/service.yaml
kubectl get pods
```

## Estrutura do projeto

```
src/
  FiapDonateReceiver.Domain/          entidades e regras de negócio (sem dependências externas)
  FiapDonateReceiver.Infrastructure/  EF Core, migrations, persistência
  FiapDonateReceiver.Worker/          host ASP.NET Core + consumer MassTransit + /health /metrics
tests/
  FiapDonateReceiver.Domain.Tests/
  FiapDonateReceiver.Infrastructure.Tests/
k8s/                                  manifests Kubernetes (Deployment, Service, ConfigMap, Secret)
docs/superpowers/specs/               documento de design
docs/superpowers/plans/               este plano de implementação
```
```

- [ ] **Step 2: Run the full end-to-end check described in the README**

Follow the README's "Subindo a infraestrutura localmente" and "Testando o fluxo fim a fim" sections yourself: bring up `docker compose`, apply migrations, insert a test `Campanhas` row, run the Worker, publish a message via the RabbitMQ management UI, and confirm via `psql` that the campaign's `ValorArrecadado` increased and a `Doacoes` row with `Status = Creditada` was created.
Expected: the campaign row's `ValorArrecadado` increases by exactly the published `valorDoacao`, and re-publishing the same `doacaoId` does **not** increase it again (idempotency).

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add README with local setup and end-to-end verification steps"
```

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

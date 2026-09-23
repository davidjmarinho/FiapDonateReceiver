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

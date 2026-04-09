# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivos de proyecto para cache de capas
COPY ["src/DuneArrakis.Domain/DuneArrakis.Domain.csproj", "src/DuneArrakis.Domain/"]
COPY ["src/DuneArrakis.SimulationService/DuneArrakis.SimulationService.csproj", "src/DuneArrakis.SimulationService/"]

# Restore
RUN dotnet restore "src/DuneArrakis.SimulationService/DuneArrakis.SimulationService.csproj"

# Copiar resto del código
COPY . .

# Build & Publish
RUN dotnet publish "src/DuneArrakis.SimulationService/DuneArrakis.SimulationService.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Final ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 80

# Railway inyecta PORT, AspNet usa PORT para configurar las URLs automáticamente
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production

# Copiar binarios
COPY --from=build /app/publish .

# Crear usuario para seguridad
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "DuneArrakis.SimulationService.dll"]

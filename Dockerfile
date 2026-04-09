# ── Stage 1: Restore & Build ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar project files primero para aprovechar cache de capas
COPY src/DuneArrakis.Domain/DuneArrakis.Domain.csproj               src/DuneArrakis.Domain/
COPY src/DuneArrakis.SimulationService/DuneArrakis.SimulationService.csproj  src/DuneArrakis.SimulationService/
COPY tests/DuneArrakis.Tests/DuneArrakis.Tests.csproj               tests/DuneArrakis.Tests/

# Restore NuGet packages
RUN dotnet restore src/DuneArrakis.SimulationService/DuneArrakis.SimulationService.csproj

# Copiar todo el código fuente
COPY . .

# Publicar en Release
RUN dotnet publish src/DuneArrakis.SimulationService/DuneArrakis.SimulationService.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    --nologo

# ── Stage 2: Runtime (imagen mínima) ──────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Crear usuario no-root por seguridad
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

COPY --from=build /app/publish .

# Railway inyecta PORT via variable de entorno
ENV ASPNETCORE_URLS=http://+:${PORT:-5000}
ENV ASPNETCORE_ENVIRONMENT=Production

# Cambiar al usuario no-root
USER appuser

EXPOSE 5000

ENTRYPOINT ["dotnet", "DuneArrakis.SimulationService.dll"]

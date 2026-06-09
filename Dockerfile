# ── Build stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Copy solution and project files first for layer-cache-friendly restore
COPY LinguaCoach.slnx ./
COPY src/LinguaCoach.Domain/LinguaCoach.Domain.csproj                   src/LinguaCoach.Domain/
COPY src/LinguaCoach.Application/LinguaCoach.Application.csproj         src/LinguaCoach.Application/
COPY src/LinguaCoach.Persistence/LinguaCoach.Persistence.csproj         src/LinguaCoach.Persistence/
COPY src/LinguaCoach.Infrastructure/LinguaCoach.Infrastructure.csproj   src/LinguaCoach.Infrastructure/
COPY src/LinguaCoach.Api/LinguaCoach.Api.csproj                         src/LinguaCoach.Api/

RUN dotnet restore src/LinguaCoach.Api/LinguaCoach.Api.csproj

# Copy remaining source and publish
COPY src/ src/
RUN dotnet publish src/LinguaCoach.Api/LinguaCoach.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Runtime stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Used by the Docker health check.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Non-root user for security
RUN groupadd --system appgroup && useradd --system --gid appgroup appuser \
    && mkdir -p /app/audio-data && chown appuser:appgroup /app/audio-data
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "LinguaCoach.Api.dll"]

# syntax=docker/dockerfile:1

# Set the .NET version based on the csproj (uses 10.0)
ARG DOTNET_VERSION=10.0-preview

# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS builder
WORKDIR /src

# Copy csproj files and restore as distinct layers for better caching
COPY --link Api.csproj ./
COPY --link ../Engine/Engine.csproj ../Engine/

# Restore dependencies (use cache mounts for speed)
RUN --mount=type=cache,target=/root/.nuget/packages \
    --mount=type=cache,target=/root/.cache/msbuild \
    dotnet restore Api.csproj

# Copy the rest of the source code
COPY --link . .
COPY --link ../Engine ../Engine

# Publish the application (self-contained, trimmed, ready for runtime)
RUN dotnet publish Api.csproj -c Release -o /app/publish --no-restore

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

# Create a non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

# Copy published output from builder
COPY --from=builder /app/publish .

# Expose the default ASP.NET port
EXPOSE 8080

# Use non-root user
USER appuser

# Set environment variables for ASP.NET
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "Metrics.Api.dll"]

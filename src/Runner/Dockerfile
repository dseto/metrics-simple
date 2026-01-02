# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0-preview

# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS builder
WORKDIR /src

# Copy csproj and restore as distinct layers for better caching
COPY --link Runner.csproj ./
COPY --link ../Engine/Engine.csproj ../Engine/

# Restore dependencies
RUN --mount=type=cache,target=/root/.nuget/packages \
    --mount=type=cache,target=/root/.cache/msbuild \
    dotnet restore "Runner.csproj"

# Copy the rest of the source code
COPY --link . .
COPY --link ../Engine ../Engine

# Publish the application (self-contained, trim unnecessary files)
RUN dotnet publish "Runner.csproj" -c Release -o /app/publish --no-restore

# --- Final Stage ---
FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION} AS final
WORKDIR /app

# Security: create non-root user
RUN addgroup --system runner && adduser --system --ingroup runner runneruser
USER runneruser

# Copy published output from builder
COPY --from=builder /app/publish .

# Set environment variables
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Metrics.Runner.dll"]

# syntax=docker/dockerfile:1

ARG DOTNET_VERSION=10.0-preview

# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS builder
WORKDIR /src

# Copy build props first
COPY Directory.Build.props .
COPY global.json .

# Copy csproj and restore as distinct layers for better caching
COPY src/Runner/Runner.csproj ./Runner/
COPY src/Engine/Engine.csproj ./Engine/

# Restore dependencies
RUN --mount=type=cache,target=/root/.nuget/packages \
    --mount=type=cache,target=/root/.cache/msbuild \
    dotnet restore "Runner/Runner.csproj"

# Copy the rest of the source code
COPY src/Runner ./Runner/
COPY src/Engine ./Engine/

# Publish the application (self-contained, trim unnecessary files)
RUN dotnet publish "Runner/Runner.csproj" -c Release -o /app/publish

# --- Final Stage ---
FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION} AS final
WORKDIR /app

# Copy published output from builder
COPY --from=builder /app/publish .

# Set environment variables
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Metrics.Runner.dll"]

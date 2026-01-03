# syntax=docker/dockerfile:1

# Set the .NET version based on the csproj (uses 10.0)
ARG DOTNET_VERSION=10.0-preview

# --- Build Stage ---
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS builder
WORKDIR /src

# Copy build props first
COPY Directory.Build.props .
COPY global.json .

# Copy csproj files and restore as distinct layers for better caching
COPY src/Api/Api.csproj ./Api/
COPY src/Engine/Engine.csproj ./Engine/

# Restore dependencies (use cache mounts for speed)
RUN --mount=type=cache,target=/root/.nuget/packages \
    --mount=type=cache,target=/root/.cache/msbuild \
    dotnet restore Api/Api.csproj

# Copy the rest of the source code
COPY src/Api ./Api/
COPY src/Engine ./Engine/

# Publish the application (self-contained, trimmed, ready for runtime)
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

# Copy published output from builder
COPY --from=builder /app/publish .

# Expose the default ASP.NET port
EXPOSE 8080

# Set environment variables for ASP.NET
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Metrics.Api.dll"]

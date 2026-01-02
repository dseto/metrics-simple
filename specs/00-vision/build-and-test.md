# Build & Test Contract

## Pré-requisitos
- .NET SDK 8.x+
- Node.js (somente se o Studio tiver build local)
- `config/secrets.local.json` local (não versionado)

## Comandos
```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

## Runner (exemplos)
```powershell
dotnet run --project src/Runner -- validate --processId <id> --version <ver> --secrets .\config\secrets.local.json --db .\config\config.db
dotnet run --project src/Runner -- run --processId <id> --version <ver> --dest local --outPath .\exports --secrets .\config\secrets.local.json --db .\config\config.db
```

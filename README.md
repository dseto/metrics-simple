## Como rodar com Docker

Este projeto inclui suporte completo para execução via Docker e Docker Compose, utilizando imagens .NET 8.0.x e SQLite.

### Requisitos específicos
- **.NET SDK/ASP.NET:** Versão 8.0.11 (conforme `Api.csproj` e Dockerfiles)
- **SQLite:** Persistência local via container `nouchka/sqlite3:latest`, com o arquivo de banco em `src/Api/config/config.db`

### Serviços e portas
- **csharp-api** (ASP.NET Core):
  - Porta exposta: `8080` (mapeada para o host)
  - Depende do serviço `sqlite`
- **csharp-runner** (CLI):
  - Não expõe portas
  - Depende do serviço `sqlite`
- **sqlite** (SQLite DB):
  - Persistência: volume local `./src/Api/config:/data`
  - Banco: `/data/config.db`

### Variáveis de ambiente
- Os containers já definem variáveis essenciais:
  - `ASPNETCORE_URLS=http://+:8080`
  - `DOTNET_RUNNING_IN_CONTAINER=true`
- Para configurações adicionais, utilize arquivos `.env` em `./src/Api` ou `./src/Runner` (opcional, descomentando no `docker-compose.yml`)

### Instruções de build e execução
1. Certifique-se de que o Docker e o Docker Compose estão instalados.
2. Execute na raiz do projeto:
   ```sh
   docker compose up --build
   ```
   Isso irá:
   - Construir as imagens dos serviços `csharp-api` e `csharp-runner` usando os Dockerfiles específicos
   - Inicializar o banco SQLite e mapear o volume para persistência
   - Expor a API em `http://localhost:8080`

### Configuração especial
- O banco SQLite é compartilhado entre API e runner via volume local (`src/Api/config/config.db`).
- Para persistência, não remova o diretório `src/Api/config`.
- Para customizar variáveis, crie um arquivo `.env` conforme necessidade e descomente a linha `env_file` no `docker-compose.yml`.

> Consulte os contratos e exemplos em `specs/shared/` para integração e uso da API.

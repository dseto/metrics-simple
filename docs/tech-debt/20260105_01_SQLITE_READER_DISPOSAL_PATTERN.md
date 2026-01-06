# SQLite Reader Disposal Pattern

**Data de Criação**: 2026-01-05
**Severity**: ALTO
**Status**: DONE
**Área**: Backend

## Descrição

O padrão de execução de múltiplos comandos SQL em uma mesma conexão SQLite causava deadlock. A solução foi refatorar para usar blocos `using` explícitos que garantem a disposição do DataReader antes de executar o próximo comando.

## Por que é um débito?

Durante a investigação do erro 500 no endpoint `/api/v1/connectors`, descobrimos que o código original tentava:
1. Executar query de connectors
2. Dentro do loop do DataReader, executar query de secrets (segunda conexão/comando)
3. SQLite não permite múltiplos comandos simultâneos na mesma conexão

A solução foi corrigir o padrão de acesso a dados, mas expôs a necessidade de revisar TODOS os repositórios para garantir conformidade com este padrão.

## Impacto

- **Antes**: Timeout/deadlock ao chamar endpoints que envolvem múltiplas queries
- **Depois**: Queries isoladas com disposição explícita
- **Risco residual**: Outros repositórios podem ter o mesmo padrão

## Como resolver

- [x] Corrigir `ConnectorRepository.GetAllConnectorsAsync()`
- [x] Corrigir `ConnectorRepository.GetConnectorByIdAsync()`
- [ ] Auditar outros repositórios (ProcessRepository, ProcessVersionRepository, etc)
- [ ] Criar utility helper para padronizar pattern
- [ ] Adicionar testes de integração validando múltiplas queries

## Dependências

Nenhuma — débito isolado.

## Estimativa

- Correção imediata: ✅ 1h (DONE)
- Auditoria completa: 2h
- Helper/padronização: 1h

## Notas e Progresso

**2026-01-05**: 
- Identificado durante debug de erro 500
- Padrão corrigido em ConnectorRepository
- Teste: 141/141 passando
- Docker validado com HTTP 200 OK
- **Commit**: [TBD - adicionar hash quando merged]

**Próximos passos**:
- Revisar `src/Api/ProcessRepository.cs`
- Revisar `src/Api/ProcessVersionRepository.cs`
- Criar pattern guide em docs para novos repositórios

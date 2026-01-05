# language: pt
@e2e @v1_2_0
Funcionalidade: Versões de Processo (ProcessVersions)
  Como usuário autenticado
  Quero listar, criar e editar versões de processo
  Para controlar evolução do pipeline

  Contexto:
    Dado que eu estou autenticado como "ADMIN_USERNAME"
    E existe um processo "Processo E2E"

  Cenário: Listar versões (GET /api/v1/processes/{processId}/versions)
    Dado que existe a versão "1" para o processo "Processo E2E"
    Quando eu abro o processo "Processo E2E"
    Então eu devo ver a versão "1" na lista de versões

  Cenário: Criar versão GET (POST /api/v1/processes/{processId}/versions)
    Quando eu abro o processo "Processo E2E"
    E eu clico em "Nova Versão"
    E eu informo a versão "1"
    E eu marco "enabled" como verdadeiro
    E eu configuro sourceRequest method "GET"
    E eu configuro sourceRequest path "/example"
    E eu informo o DSL profile "jsonata"
    E eu informo o DSL text "$"
    E eu clico em "Salvar Versão"
    Então eu devo ver a versão "1" na lista de versões

  Cenário: Criar versão POST com body e contentType
    Quando eu abro o processo "Processo E2E"
    E eu clico em "Nova Versão"
    E eu informo a versão "2"
    E eu configuro sourceRequest method "POST"
    E eu configuro sourceRequest path "/submit"
    E eu informo sourceRequest contentType "application/json"
    E eu informo sourceRequest body:
      | json |
      | {"a":1,"b":"x"} |
    E eu informo o DSL profile "jsonata"
    E eu informo o DSL text "$"
    E eu clico em "Salvar Versão"
    Então eu devo ver a versão "2" na lista de versões

  Cenário: Atualizar versão (PUT /api/v1/processes/{processId}/versions/{version})
    Dado que existe a versão "1" para o processo "Processo E2E"
    Quando eu abro a versão "1"
    E eu desmarco "enabled"
    E eu clico em "Salvar Versão"
    Então a versão "1" deve estar com enabled igual a falso

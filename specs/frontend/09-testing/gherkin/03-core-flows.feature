# language: pt
@e2e @v1_2_0
Funcionalidade: Fluxos principais (Core Flows)
  Como usuário autenticado
  Quero executar o fluxo básico
  Para validar o funcionamento end-to-end

  Contexto:
    Dado que eu estou autenticado como "ADMIN_USERNAME"

  Cenário: Criar processo, criar versão e executar preview
    Quando eu acesso a tela "Processos"
    E eu clico em "Novo Processo"
    E eu informo o id "processo-core"
    E eu informo o nome "Processo Core"
    E eu clico em "Salvar"
    Então eu devo ver o processo "processo-core" na lista

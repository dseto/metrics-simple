# language: pt
@e2e @v1_2_0
Funcionalidade: Autorização
  Como administrador
  Quero ver funcionalidades de admin
  Para gerenciar processos e conectores

  Contexto:
    Dado que eu estou autenticado como "ADMIN_USERNAME"

  Cenário: Usuário admin pode acessar Conectores
    Quando eu acesso a tela "Conectores"
    Então eu devo ver a lista de conectores

  Cenário: Usuário sem permissão recebe 403
    Dado que eu estou autenticado como "READER_USERNAME"
    Quando eu acesso a tela "Conectores"
    Então eu devo ver uma mensagem de "Acesso negado"

# language: pt
@e2e @v1_2_0
Funcionalidade: Autenticação
  Como usuário
  Quero autenticar na aplicação
  Para acessar as telas protegidas

  Cenário: Login com credenciais válidas
    Dado que eu estou na tela de "Login"
    Quando eu informo usuário "ADMIN_USERNAME"
    E eu informo senha "ADMIN_PASSWORD"
    E eu clico em "Entrar"
    Então eu devo ser redirecionado para a tela "Processos"

  Cenário: Acesso a rota protegida sem sessão deve redirecionar para Login
    Dado que eu não estou autenticado
    Quando eu acesso a rota "/processes"
    Então eu devo ser redirecionado para a tela de "Login"

  Cenário: Token expirado deve forçar novo login
    Dado que eu estou autenticado
    E meu token expira
    Quando eu acesso a tela "Conectores"
    Então eu devo ser redirecionado para a tela de "Login"

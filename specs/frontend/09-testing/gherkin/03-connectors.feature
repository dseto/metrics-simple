# language: pt
@e2e @v1_2_0
Funcionalidade: Conectores
  Como usuário autenticado
  Quero criar, editar e remover conectores
  Para usar nos processos

  Contexto:
    Dado que eu estou autenticado como "ADMIN_USERNAME"

  @smoke
  Cenário: Listar conectores (GET /api/v1/connectors)
    Quando eu acesso a tela "Conectores"
    Então eu devo ver a lista de conectores

  Cenário: Criar conector com Bearer token (POST /api/v1/connectors)
    Quando eu acesso a tela "Conectores"
    E eu clico em "Novo Conector"
    E eu informo id "conn-bearer"
    E eu informo name "Connector Bearer"
    E eu informo baseUrl "https://api.example.com"
    E eu seleciono authType "BEARER"
    E eu informo apiToken "<redacted>"
    E eu clico em "Salvar"
    Então eu devo ver o conector "conn-bearer" na lista
    E o conector "conn-bearer" deve exibir indicador "Token configurado"

  Cenário: Criar conector com API Key em Header
    Quando eu acesso a tela "Conectores"
    E eu clico em "Novo Conector"
    E eu informo id "conn-apikey"
    E eu informo name "Connector API Key"
    E eu informo baseUrl "https://api.example.com"
    E eu seleciono authType "API_KEY"
    E eu seleciono apiKeyLocation "HEADER"
    E eu informo apiKeyName "X-API-Key"
    E eu informo apiKeyValue "<redacted>"
    E eu clico em "Salvar"
    Então eu devo ver o conector "conn-apikey" na lista
    E o conector "conn-apikey" deve exibir indicador "API Key configurada"

  Cenário: Manter Bearer token ao editar sem informar token (PUT /api/v1/connectors/{id})
    Dado que existe um conector "conn-bearer" com token configurado
    Quando eu edito o conector "conn-bearer"
    E eu altero o timeoutSeconds para "45"
    E eu não informo apiToken
    E eu clico em "Salvar"
    Então o conector "conn-bearer" deve continuar com indicador "Token configurado"

  Cenário: Remover Bearer token (PUT /api/v1/connectors/{id})
    Dado que existe um conector "conn-bearer" com token configurado
    Quando eu edito o conector "conn-bearer"
    E eu clico em "Limpar token"
    E eu clico em "Salvar"
    Então o conector "conn-bearer" não deve exibir indicador "Token configurado"

  Cenário: Deletar conector (DELETE /api/v1/connectors/{id})
    Dado que existe um conector "conn-to-delete"
    Quando eu acesso a tela "Conectores"
    E eu clico em "Deletar" no conector "conn-to-delete"
    Então o conector "conn-to-delete" não deve mais aparecer na lista

  Cenário: Deletar conector em uso deve mostrar erro (409)
    Dado que existe um conector "conn-in-use"
    E existe um processo que utiliza o conector "conn-in-use"
    Quando eu clico em "Deletar" no conector "conn-in-use"
    Então eu devo ver uma mensagem contendo "Conector em uso"

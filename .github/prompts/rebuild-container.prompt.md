---
agent: spec-driven-dockerizer
---
Executar o build completo do projeto e recriar o container Docker utilizando a configuração existente no repositório.

## Instruções obrigatórias

1. Analise a estrutura atual do repositório antes de executar qualquer comando.
2. Execute o build do projeto conforme definido (ex: `dotnet build`, `npm run build`, etc.).
3. Utilize **exclusivamente** o `Dockerfile` existente no repositório.
4. Gere uma nova imagem Docker a partir do `Dockerfile` atual.
5. Caso exista um container anterior em execução:
   - Pare o container
   - Remova o container antigo
6. Crie e publique um novo container usando a imagem recém-gerada.
7. Utilize a tag adequada (`latest` ou a versão definida no projeto).
8. Não crie arquivos novos nem altere configurações sem justificativa explícita.

## Validações obrigatórias

Após a publicação, valide:

- Build concluído com sucesso
- Imagem Docker criada corretamente
- Container em estado **running**
- Logs iniciais do container sem erros críticos de build ou runtime

## Critério de sucesso

O processo será considerado bem-sucedido somente se:

- O container subir sem erros
- A aplicação estiver acessível ou executando conforme esperado
- Não existirem falhas críticas nos logs iniciais

Se qualquer etapa falhar, interrompa o processo e reporte claramente o erro encontrado.
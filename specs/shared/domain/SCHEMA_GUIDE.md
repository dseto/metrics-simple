# JSON Schema Guide (Shared Contracts)

Data: 2026-01-02  
Escopo: `specs/shared/domain/schemas/*.schema.json`

## Objetivo
Padronizar:
- como usar `$ref` (caminhos relativos)
- como resolver `$ref` em runtime e nos testes
- quando extrair um sub-objeto para um schema separado

---

## Resolução de `$ref`

### Convenção de caminhos
- Referências **devem ser relativas** ao diretório do schema que contém o `$ref`.
- Use:
  - Mesmo diretório: `"$ref": "id.schema.json"`
  - Subdiretório: `"$ref": "subdir/arquivo.schema.json"`
- Evitar paths absolutos e URLs.

### Regra prática
> Se um schema `A.schema.json` referencia `B.schema.json`, então **`B` deve estar ao lado de `A`** (ou no subdiretório referenciado).

Exemplo real (este deck):
- `connector.schema.json` → `"$ref": "id.schema.json"`

---

## Como resolver `$ref` em runtime (backend .NET)

### Recomendação (testes e runtime)
Para bibliotecas que dependem de “document path” (ex.: NJsonSchema), **carregue por arquivo** e não apenas por string:

```csharp
// ✅ Preserva o documentPath e permite resolver $ref relativo
var schema = await JsonSchema.FromFileAsync(schemaPath);
```

Se o schema for carregado como string (ex.: `FromJsonAsync(string)`), configure um resolver com basePath do diretório:

```csharp
var settings = new JsonSchemaReaderSettings
{
  SchemaResolver = new JsonSchemaResolver(new JsonReferenceResolver(baseDirectory))
};
var schema = await JsonSchema.FromJsonAsync(schemaJson, settings);
```

> O ponto principal: **o resolver precisa saber o diretório base** onde estão os schemas referenciados.

---

## Como resolver `$ref` em testes

### Estratégia recomendada
1) Copie **todos** os schemas de `specs/shared/domain/schemas/` para um diretório único em `bin/.../schemas/`.
2) Valide usando loader “file-based” (`FromFileAsync`) apontando para o arquivo principal.

Isso garante que refs relativos sejam resolvidos no mesmo diretório.

---

## Granularidade dos schemas (inline vs arquivo separado)

### Regra
- Um schema separado é recomendado quando:
  - o objeto é referenciado por **2+ schemas**
  - o objeto tem evolução independente e precisa de versionamento/IDs próprios
- Um objeto pode ficar **inline** quando:
  - é usado apenas dentro de **um** schema pai
  - não há necessidade de reutilização

### Exemplo
- ✅ `id.schema.json` → separado (reutilizado por vários schemas)
- ✅ `sourceRequest` → inline em `processVersion.schema.json` (usado apenas ali)

---

## Compatibilidade
- Os schemas deste deck seguem **JSON Schema 2020-12**.
- Extensões proprietárias (se existirem) devem usar prefixo `x-`.


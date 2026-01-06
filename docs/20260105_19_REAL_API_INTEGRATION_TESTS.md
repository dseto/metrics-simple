# IT10 — Real API Integration Tests (20260105_19)

## Objetivo
Criar testes de integração reais usando o endpoint `/api/v1/preview/transform` com dados da API HGBrasil Weather para validar transformações Jsonata.

## Status
✅ **COMPLETO** — 6 testes passando

## Testes Implementados

### 1. PreviewTransform_SimpleExtraction_Returns200
- **Descrição**: Extração simples de campos (city, temperature)
- **DSL**: `{"city": results.city_name, "current_temp": results.temp}`
- **Status**: ✅ Passou

### 2. PreviewTransform_WithAggregation_Returns200
- **Descrição**: Funções de agregação ($average, $count)
- **DSL**: `{"city": results.city_name, "avg_high": $average(results.forecast.max), "count": $count(results.forecast)}`
- **Status**: ✅ Passou

### 3. PreviewTransform_WithComplexArithmetic_Returns200
- **Descrição**: Aritmética complexa em collections com duplos parênteses
- **DSL**: `{"city": results.city_name, "avg_mean_temp": $average(results.forecast.((max + min) / 2))}`
- **Status**: ✅ Passou

### 4. PreviewTransform_WithFilter_Returns200
- **Descrição**: Filtragem de arrays com predicado
- **DSL**: `{"city": results.city_name, "rainy_days": results.forecast[condition="rain"].{"date": date, "rain_mm": rain}}`
- **Status**: ✅ Passou

### 5. PreviewTransform_WithArrayMapping_Returns200
- **Descrição**: Mapeamento de arrays com transformação de campos
- **DSL**: `{"city": results.city_name, "forecast": results.forecast.{"date": date, "mean_temp": (max + min) / 2}}`
- **Status**: ✅ Passou

### 6. PreviewTransform_InvalidDsl_ReturnsValidationError
- **Descrição**: Validação de erro em DSL inválido
- **DSL**: `$()$()$(` (unclosed function calls)
- **Status**: ✅ Passou (retorna IsValid=false com mensagens de erro)

## Aprendizados Técnicos

### 1. Autenticação em Testes
- Endpoint `/api/v1/preview/transform` requer Bearer token
- Implementado padrão: `httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token)`
- Token obtido via `/api/auth/token` com credenciais `admin:testpass123`

### 2. Schema Validation
- **Problema**: `#/: ObjectExpected` erro na validação
- **Causa**: OutputSchema deve ser `array` pois a normalização sempre retorna `JsonElement.Array`
- **Solução**: 
  ```json
  {
    "type": "array",
    "items": { "type": "object", "properties": {...} }
  }
  ```

### 3. Dados Reais HGBrasil
Estrutura utilizada:
- `results.city_name` — string (cidade)
- `results.temp` — number (temperatura atual)
- `results.forecast[]` — array com:
  - `date`, `weekday`, `max`, `min`, `humidity`, `rain`, `rain_probability`, `description`, `condition`

### 4. Padrão de Teste xUnit
- Herança de `IAsyncLifetime` para inicializar/limpar
- Uso de `TestWebApplicationFactory` com `disableAuth: false` para habilitar validação
- Uso de `WebApplicationFactory<Program>` para rodar API em-processo

## Arquivos Criados/Modificados

### Criado
- `tests/Integration.Tests/IT10_PreviewTransformRealApiTests.cs` (350 linhas)
  - 6 testes xUnit com padrão fluente
  - Dados reais HGBrasil JSON embarcados
  - Validação completa com FluentAssertions

## Execução

```bash
# Rodar todos os IT10
dotnet test --filter "IT10_PreviewTransformRealApiTests"

# Resultado
Total: 6
Aprovados: 6
Falhados: 0
```

## Próximos Passos

1. Validar que melhorias no LLM prompt (BuildSystemPrompt com structure analysis) produzem DSLs corretos
2. Integrar com testes de geração de DSL via LLM (IT05)
3. Considerar adicionar mais casos edge (null handling, tipo de dados mismatch, etc)

## Relacionado

- Melhorias LLM: `src/Api/AI/HttpOpenAiCompatibleProvider.cs`
- Contrato OpenAPI: `specs/shared/openapi/config-api.yaml`
- Schema: `specs/shared/domain/schemas/previewRequest.schema.json`
- Engine: `src/Engine/Engine.cs`

---
**Data**: 2026-01-05
**Autor**: GitHub Copilot
**Versão**: 1.0

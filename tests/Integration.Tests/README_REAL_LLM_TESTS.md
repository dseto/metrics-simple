# Testes Reais de LLM - IT05

## O que são?

Os testes `IT05_RealLlmIntegrationTests` fazem **chamadas reais** a uma API de LLM configurável (padrão: OpenRouter) e validam que:

1. **A LLM retorna DSL válido** (Jsonata, não vazio, bem formado)
2. **O DSL gerado consegue executar** (preview validation passa)
3. **O resultado é semanticamente correto** (valores numéricos, estrutura esperada, filtros aplicados)

Se qualquer uma dessas validações falhar, o teste **FALHA imediatamente**.

## Como Rodar

### Opção 1: Sem API Key (Tests Skipped)
```bash
dotnet test Metrics.Simple.SpecDriven.sln
```
Resultado: Todos os testes passam, IT05 é SKIPPED com mensagem:
```
SkipTestException: Real LLM tests require API key. Set METRICS_OPENROUTER_API_KEY 
or OPENROUTER_API_KEY environment variable.
```

### Opção 2: Com OpenRouter API Key (Tests Run & Validate)

```bash
# Windows PowerShell
$env:METRICS_OPENROUTER_API_KEY = "sk-or-..."
dotnet test Metrics.Simple.SpecDriven.sln

# Ou via appsettings.json
```

Resultado: 4 testes reais executam contra OpenRouter:
- ✅ IT05-01: Metric calculation (decimal → percentage)
- ✅ IT05-02: Text extraction (regex/parsing)
- ✅ IT05-03: Field rename + filtering
- ✅ IT05-04: Mathematical aggregation

## Como Funciona (Internamente)

### Fluxo de cada teste:

```
1. Construir RequestDTO
   ├─ GoalText (descrição em português)
   ├─ SampleInput (dados de entrada)
   ├─ OutputSchema (estrutura esperada)
   └─ Constraints (limites)

2. POST /api/ai/dsl/generate
   → OpenRouter (LLM real) recebe request
   → Retorna DslGenerateResult com Jsonata DSL

3. Validações:
   ├─ HTTP 200 OK ✓
   ├─ DSL não vazio ✓
   ├─ DSL.profile == "jsonata" ✓
   └─ LLM retornou rationale + warnings ✓

4. POST /api/preview/transform
   → Executa DSL contra SampleInput
   → Valida output contra OutputSchema
   → Retorna PreviewOutput

5. Validações de resultado:
   ├─ Preview passou ✓
   ├─ Dados transformados corretamente ✓
   ├─ Valores numéricos estão certos ✓
   └─ Filtros foram aplicados ✓

6. SE alguma validação falhar
   → Test FAILS com assertion error detalhado
```

## Cenários de Falha (Esperados)

### ❌ Teste Falhará Se:

1. **LLM retorna DSL inválido**
   ```csharp
   // DSL que não consegue executar
   result.Dsl.Text = "invalid jsonata {{{";
   // Preview falha → Test FAILS
   ```

2. **LLM ignora transformação solicitada**
   ```csharp
   // Goal: "rename host → hostname"
   // LLM retorna: DSL que mantém "host"
   // Assertion: hostname1.GetString().Should().Be("server-01")
   // FAILS: Property 'hostname' not found
   ```

3. **Valores calculados estão errados**
   ```csharp
   // Goal: "convert decimal CPU to percentage (×100)"
   // LLM retorna: $v.cpu (sem multiplicar)
   // Assertion: cpuValue.Should().BeGreaterThan(40) // 0.45 < 40
   // FAILS: 0.45 is not greater than 40
   ```

4. **Filtragem não aplicada**
   ```csharp
   // Goal: "filter out inactive users"
   // LLM retorna: DSL sem filtro
   // Assertion: outputArray.GetArrayLength().Should().Be(2) // esperado
   // FAILS: Got 3, not 2
   ```

## Testes Inclusos

### IT05-01: CPU Metric Transformation
- **Input**: CPU em decimal (0.0-1.0)
- **Output**: CPU em percentual (0-100), hostname renomeado
- **Validações**:
  - 0.45 → 45% ✓
  - 0.12 → 12% ✓
  - host → hostname ✓

### IT05-02: Log Text Extraction
- **Input**: String de log com valores inline
  - "Memory: 512MB, CPU: 10%, Status: healthy"
- **Output**: Extração estruturada
- **Validações**:
  - memoryMB == 512 ✓
  - cpuPercent == 10 ✓
  - status == "healthy" ✓

### IT05-03: Rename + Filter
- **Input**: 3 usuários, 1 inativo
- **Output**: 2 usuários (inativo removido), fullName combinado
- **Validações**:
  - Array tem 2 itens (não 3) ✓
  - fullName contém "John Doe" ✓
  - fullName contém "Jane Smith" ✓

### IT05-04: Math Aggregation
- **Input**: 3 produtos com quantity × price
- **Output**: Totais e médias calculadas
- **Validações**:
  - totalQuantity = 10+5+15 = 30 ✓
  - totalRevenue ≈ 3250 ✓
  - Cálculos batendo byte-a-byte ✓

## Configuração

### appsettings.json (defaults)
```json
{
  "AI": {
    "Enabled": true,
    "Provider": "HttpOpenAICompatible",
    "EndpointUrl": "https://openrouter.ai/api/v1/chat/completions",
    "ApiKey": "",
    "Model": "openai/gpt-4o-mini"
  }
}
```

### Environment Variable (Precedência > appsettings)
```bash
$env:METRICS_OPENROUTER_API_KEY = "sk-or-xxxxxxxxxxxx"
# ou
export OPENROUTER_API_KEY="sk-or-xxxxxxxxxxxx"
```

## Troubleshooting

### "Tests are skipped"
→ API key não configurada. Configure env var ou appsettings.

### "StatusCode: 429 - Rate Limited"
→ OpenRouter rate limit atingido. Aguarde alguns minutos e retente.

### "StatusCode: 200, but output invalid"
→ LLM retornou JSON malformado. Verifique:
```
1. Response parsing failures (check logs)
2. Schema validation errors (PreviewOutput)
3. Assertion errors (valores incorretos)
```

### "Assertion: cpuPercent is not greater than 40"
→ LLM não aplicou a transformação solicitada. Verifique:
- GoalText é claro?
- SampleInput é representativo?
- OutputSchema define tipos corretamente?

## Integration com CI/CD

### GitHub Actions / Azure Pipelines
```yaml
- name: Run LLM Integration Tests
  env:
    METRICS_OPENROUTER_API_KEY: ${{ secrets.OPENROUTER_API_KEY }}
  run: dotnet test --filter "FullyQualifiedName~IT05"
```

### Comportamento esperado:
- ✅ Sem API key em CI: Tests SKIPPED (não falha)
- ✅ Com API key em CI: Tests RUN, devem passar (ou FAIL se LLM ruim)
- ❌ Se test fai: CI build ❌ (DSL inválido detectado)

## Próximas Melhorias

- [ ] Adicionar testes para edge cases (muito grandes inputs)
- [ ] Validar Jsonata syntax com parser externo
- [ ] Coletar métricas: latência LLM, taxa de sucesso
- [ ] Comparar com golden baselines
- [ ] Suporte para outras LLMs (não só OpenRouter)

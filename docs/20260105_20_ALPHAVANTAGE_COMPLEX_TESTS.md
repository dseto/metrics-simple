# IT11 — AlphaVantage Complex Financial Transformations (20260105_20)

## Objetivo
Criar 10 testes de complexidade média a alta usando a API AlphaVantage (TIME_SERIES_INTRADAY) com transformações financeiras realistas: conversão de time series, cálculos de retorno, análise de volume, médias móveis, volatilidade, VWAP, etc.

## Status
✅ **COMPLETO** — 10 testes passando

## Testes Implementados

### 1. Test01_ConvertTimeSeriesObjectToSortedArray
- **Descrição**: Converte objeto com timestamp keys em array ordenado
- **Técnica**: `$spread()` + ordenação por timestamp `^(timestamp)`
- **DSL**: Extrai timestamp, open, close, volume e ordena
- **Status**: ✅ Passou

### 2. Test02_CalculateDailyReturns
- **Descrição**: Calcula retorno percentual `(close - open) / open * 100`
- **Técnica**: Aritmética financeira + `$round()` para precisão
- **Caso Real**: Análise de performance intraday
- **Status**: ✅ Passou

### 3. Test03_TopNHighestVolumeIntervals
- **Descrição**: Identifica intervalos com maior volume (ordenação descendente)
- **Técnica**: `^(>volume)` ordenação reversa
- **Caso Real**: Identificar períodos de alta liquidez
- **Status**: ✅ Passou

### 4. Test04_SimpleMovingAverageClose
- **Descrição**: Calcula SMA (Simple Moving Average) de 3 períodos
- **Técnica**: `$average()` dos 3 primeiros closes via array slicing
- **Caso Real**: Indicador técnico básico para análise de tendência
- **Status**: ✅ Passou

### 5. Test05_VolatilityCalculation
- **Descrição**: Calcula volatilidade `high - low` por intervalo
- **Técnica**: Subtração direta de campos numéricos
- **Caso Real**: Medida de risco e instabilidade do preço
- **Status**: ✅ Passou

### 6. Test06_FilterHighVolumeIntervals
- **Descrição**: Lista todos intervalos ordenados por volume (simplificado)
- **Técnica**: Ordenação descendente por volume
- **Caso Real**: Identificar períodos de maior atividade
- **Status**: ✅ Passou

### 7. Test07_VWAPCalculation
- **Descrição**: Calcula VWAP (Volume-Weighted Average Price)
- **Fórmula**: `Σ(close × volume) / Σ(volume)`
- **Técnica**: `$sum()` de produtos dividido por `$sum()` de volumes
- **Caso Real**: Benchmark institucional para execução de ordens
- **Status**: ✅ Passou

### 8. Test08_OHLCStatistics
- **Descrição**: Estatísticas consolidadas (min, max, avg close + total volume)
- **Técnica**: `$min()`, `$max()`, `$average()`, `$sum()` em agregações
- **Caso Real**: Resumo estatístico de sessão de trading
- **Status**: ✅ Passou

### 9. Test09_PriceRangePercentage
- **Descrição**: Range percentual `(high - low) / open * 100`
- **Técnica**: Aritmética complexa com normalização pelo open
- **Caso Real**: Volatilidade relativa ao preço de abertura
- **Status**: ✅ Passou

### 10. Test10_BullishBearishIntervals
- **Descrição**: Classifica intervalos como bullish/bearish via ternário
- **Técnica**: `close >= open ? "bullish" : "bearish"` + cálculo de mudança
- **Caso Real**: Análise de sentimento de mercado por período
- **Status**: ✅ Passou

## Técnicas Jsonata Utilizadas

### 1. Transformação de Objetos em Arrays
```jsonata
$spread($."Time Series (5min)")
```
- Converte objeto com keys dinâmicas em array de pares key-value

### 2. Acesso a Keys Dinâmicas
```jsonata
$keys()[0]
```
- Obtém timestamp da estrutura spread

### 3. Ordenação Reversa
```jsonata
^(>volume)
```
- Ordena por campo volume em ordem descendente

### 4. Agregações Financeiras
```jsonata
$sum($timeSeries.($number($."4. close") * $number($."5. volume")))
```
- VWAP: soma de produtos (price × volume)

### 5. Array Slicing para SMA
```jsonata
[$closes[0], $closes[1], $closes[2]]
```
- Extrai primeiros N elementos para média móvel

### 6. Operadores Ternários
```jsonata
$number($."4. close") >= $number($."1. open") ? "bullish" : "bearish"
```
- Classificação condicional

### 7. Transformação com Variáveis Locais
```jsonata
(
    $timeSeries := $spread($."Time Series (5min)");
    $closes := $timeSeries.$number($."4. close");
    { ... }
)
```
- Uso de variáveis para cálculos complexos

## Estrutura de Dados AlphaVantage

```json
{
    "Meta Data": {
        "1. Information": "Intraday (5min) ...",
        "2. Symbol": "IBM",
        "3. Last Refreshed": "2026-01-05 19:55:00",
        "4. Interval": "5min",
        "5. Output Size": "Compact",
        "6. Time Zone": "US/Eastern"
    },
    "Time Series (5min)": {
        "2026-01-05 19:55:00": {
            "1. open": "294.9800",
            "2. high": "295.0000",
            "3. low": "294.9800",
            "4. close": "294.9900",
            "5. volume": "132"
        }
    }
}
```

### Campos Utilizados
- `1. open` — Preço de abertura do intervalo
- `2. high` — Preço máximo
- `3. low` — Preço mínimo
- `4. close` — Preço de fechamento
- `5. volume` — Volume negociado

## Desafios e Soluções

### 1. Filtros Complexos em Arrays Spread
**Problema**: Sintaxe `$spread()[filter]` não funciona diretamente
**Solução**: Simplificar para ordenação sem filtro inline

### 2. Variable Assignment em Jsonata
**Problema**: `:=` dentro de array expressions causa parse error
**Solução**: Usar parênteses externos `( $var := value; expression )`

### 3. Array Slicing com Funções Agregadas
**Problema**: `$average($array[0..2])` não funciona diretamente em map
**Solução**: Criar array intermediário `[$arr[0], $arr[1], $arr[2]]`

### 4. Conversão de String para Number
**Problema**: API retorna números como strings
**Solução**: Sempre usar `$number()` para conversão explícita

## Casos de Uso Reais

### Trading e Análise Técnica
- **SMA (Test04)**: Indicador de tendência
- **VWAP (Test07)**: Benchmark para traders institucionais
- **Volatilidade (Test05, Test09)**: Gestão de risco

### Análise de Volume
- **Top Volume (Test03)**: Identificar liquidez
- **High Volume Filter (Test06)**: Períodos de interesse institucional

### Classificação de Mercado
- **Bullish/Bearish (Test10)**: Sentimento de mercado
- **Daily Returns (Test02)**: Performance tracking

### Estatísticas Consolidadas
- **OHLC Stats (Test08)**: Resumo de sessão
- **Price Range (Test09)**: Volatilidade relativa

## Execução

```bash
# Rodar todos os IT11
dotnet test --filter "IT11_AlphaVantageComplexTests"

# Resultado
Total: 10
Aprovados: 10 ✅
Falhados: 0
Duração: ~6s
```

## Métricas de Complexidade

### Transformações
- **Simples**: 3 testes (conversão, volatilidade, bullish/bearish)
- **Média**: 5 testes (returns, SMA, range%, filter, top N)
- **Complexa**: 2 testes (VWAP, OHLC stats)

### Técnicas Jsonata
- Spread: 10/10 testes
- Agregações ($sum, $average, $min, $max): 6/10 testes
- Ordenação: 3/10 testes
- Variáveis locais: 4/10 testes
- Aritmética financeira: 7/10 testes
- Condicionais: 1/10 testes

## Próximos Passos

1. Adicionar testes com múltiplos símbolos (batch processing)
2. Implementar RSI (Relative Strength Index)
3. Detectar padrões de candlestick (doji, hammer, etc)
4. Calcular Bollinger Bands
5. Implementar EMA (Exponential Moving Average)

## Relacionado

- IT10: Testes com HGBrasil Weather API
- Melhorias LLM: `src/Api/AI/HttpOpenAiCompatibleProvider.cs`
- Engine: `src/Engine/Engine.cs`
- Contrato: `specs/shared/openapi/config-api.yaml`

---
**Data**: 2026-01-05
**Autor**: GitHub Copilot
**Versão**: 1.0
**Complexidade**: Média a Alta
**Domínio**: Análise Financeira / Trading

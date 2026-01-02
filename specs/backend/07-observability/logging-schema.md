
# Logging Schema (Serilog JSONL)

Data: 2026-01-01

Todos eventos devem incluir:
- timestamp, level, message
- executionId
- eventName
- step

Recomendados:
- processId, version
- durationMs, status
- errorCode, httpStatus
- bytesIn/bytesOut, csvRows/csvBytes
- correlationId

EventName mínimos:
- ExecutionStarted
- ExecutionCompleted
- ExecutionFailed
- SourceFetchStarted/Completed
- TransformStarted/Completed
- OutputValidationCompleted
- CsvGenerated
- CsvStored

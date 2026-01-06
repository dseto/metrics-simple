# DSL Template Library (Jsonata) — Catálogo

## Objetivo
Fornecer “soluções determinísticas” para casos comuns, aumentando a taxa de sucesso e reduzindo custo/latência.

## Templates

### T1 — Extract + Rename (array)
**Quando usar**: “extract/selecionar campos”, “renomear para…”, “de cada item”

**Forma**
```jsonata
<arrayPath>.{ "<alias1>": <field1>, "<alias2>": <field2> }
```

### T2 — Sort (array)
**Quando usar**: “sort/order/ordenar”

**Forma**
```jsonata
<arrayPath>^(<field>)   // asc
<arrayPath>^(><field>)  // desc
```

### T3 — Filter (array)
**Quando usar**: “where/filtrar/apenas”

**Forma**
```jsonata
<arrayPath>[<cond>].{ ... }
```

### T4 — Map translate (ternary)
**Quando usar**: “translate”, “map values”, “pending='Pendente'”

**Forma**
```jsonata
status="pending" ? "Pendente" : "Concluído"
```

### T5 — Group + Sum
**Quando usar**: “group by/agrupar por”, “total”, “sum”

**Forma**
```jsonata
$distinct(<arrayPath>.<groupField>).{
  "<groupAlias>": $,
  "<sumAlias>": $sum(<arrayPath>[<groupField>=$].(<expr>))
}
```

## Matcher (heurístico)
- group: contém “group by”/“agrupar”
- sort: contém “sort”/“order”/“ordenar”
- translate/map: contém “translate”/“map”/“pending=”
- extract: contém “extract”/“extrair”/“selecionar”

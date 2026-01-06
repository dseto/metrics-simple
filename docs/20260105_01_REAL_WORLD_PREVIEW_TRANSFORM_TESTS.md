# Real-World DSL Generation and Preview/Transform Tests

**Date**: 2026-01-05  
**Purpose**: Validate DSL generation robustness with real external API data  
**API Tested**: HGBrasil Weather API (Curitiba, PR)  
**Endpoint**: POST `/api/v1/preview/transform`  

---

## Sample Data Source

**API**: https://api.hgbrasil.com/weather?city_name=Curitiba%2CPR&key=f110205d

**Response Structure**:
```json
{
  "by": "city_name",
  "valid_key": true,
  "results": {
    "temp": 16,
    "date": "05/01/2026",
    "city_name": "Curitiba",
    "humidity": 83,
    "cloudiness": 75,
    "wind_speedy": "5.66 km/h",
    "wind_cardinal": "L",
    "forecast": [
      {
        "date": "05/01",
        "weekday": "Seg",
        "max": 25,
        "min": 13,
        "humidity": 53,
        "rain_probability": 20,
        "condition": "rain"
      },
      {
        "date": "06/01",
        "weekday": "Ter",
        "max": 23,
        "min": 13,
        "humidity": 64,
        "rain_probability": 48,
        "condition": "rain"
      }
    ]
  }
}
```

---

## Test Cases

### TC-001: Simple Path Extraction
**Goal**: Extract current temperature from `results.temp`  
**Difficulty**: ⭐ Easy  
**Tests**: Basic field navigation  
**Expected DSL**: `results.temp`  
**Expected Output**: `16`  
**Validation**: 
- ✓ Correct path recognition
- ✓ Numeric value extraction

---

### TC-002: Object Construction
**Goal**: Create object with current weather conditions (temp, humidity, city, condition)  
**Difficulty**: ⭐⭐ Medium  
**Tests**: Object construction, multiple field selection  
**Expected DSL**: `{ "temp": results.temp, "humidity": results.humidity, "city": results.city_name, "condition": results.description }`  
**Expected Output**: 
```json
{
  "temp": 16,
  "humidity": 83,
  "city": "Curitiba",
  "condition": "Tempo nublado"
}
```
**Validation**:
- ✓ Object construction syntax
- ✓ Multiple fields correctly mapped
- ✓ Field names in snake_case handled

---

### TC-003: Average Temperature from Forecast (CRITICAL)
**Goal**: Calculate average of min and max temperatures across forecast days  
**Difficulty**: ⭐⭐⭐ Hard  
**Tests**: 
- Nested array path (`results.forecast`)
- Arithmetic on collections (parentheses)
- Aggregation function
- Path analysis robustness
  
**Expected DSL**: `{ "avg_forecast_temp": $average(results.forecast.((max + min) / 2)) }`  
**Expected Output**: 
```json
{
  "avg_forecast_temp": 18.5
}
```
**Validation**:
- ✓ Correct array path (`results.forecast`, not just `forecast`)
- ✓ Double parentheses around arithmetic expression
- ✓ `$average()` aggregation function
- ✓ Proper calculation: (25+13)/2=19, (23+13)/2=18, avg=18.5

**Critical**: This test validates the LLM's ability to:
1. Analyze `results.forecast` structure in sample input
2. Understand that it's an array
3. Generate correct path in aggregation context
4. Use proper parenthesization for arithmetic

---

### TC-004: Forecast Mapping
**Goal**: Map forecast array to object with key weather data  
**Difficulty**: ⭐⭐ Medium  
**Tests**: Array mapping, field selection from nested arrays  
**Expected DSL**: `results.forecast.{ "date": date, "high": max, "low": min, "condition": condition, "rain_chance": rain_probability }`  
**Expected Output**:
```json
[
  {
    "date": "05/01",
    "high": 25,
    "low": 13,
    "condition": "rain",
    "rain_chance": 20
  },
  {
    "date": "06/01",
    "high": 23,
    "low": 13,
    "condition": "rain",
    "rain_chance": 48
  }
]
```
**Validation**:
- ✓ Array iteration with dot notation
- ✓ Object construction within map
- ✓ Field name aliasing (max→high, min→low)

---

### TC-005: Rain Statistics
**Goal**: Analyze rain probability and volume across forecast  
**Difficulty**: ⭐⭐⭐ Hard  
**Tests**: Aggregations, filtering, multiple calculations  
**Expected DSL**: `{ "max_rain_chance": $max(results.forecast.rain_probability), "avg_rain": $average(results.forecast.rain), "rainy_days": $count(results.forecast[rain_probability > 0]) }`  
**Expected Output**:
```json
{
  "max_rain_chance": 48,
  "avg_rain": 0.17,
  "rainy_days": 2
}
```
**Validation**:
- ✓ Multiple aggregation functions
- ✓ Array filtering with comparison
- ✓ Correct count of matching elements

---

### TC-006: Wind Information
**Goal**: Extract and structure wind data  
**Difficulty**: ⭐ Easy  
**Tests**: String fields, multiple types  
**Expected DSL**: `{ "speed": results.wind_speedy, "direction": results.wind_cardinal, "degrees": results.wind_direction }`  
**Expected Output**:
```json
{
  "speed": "5.66 km/h",
  "direction": "L",
  "degrees": 100
}
```
**Validation**:
- ✓ String and number fields coexist
- ✓ Field name mapping preserved

---

### TC-007: Comprehensive Statistics
**Goal**: Calculate comprehensive weather statistics  
**Difficulty**: ⭐⭐⭐ Hard  
**Tests**: Multiple aggregations, nested array access, comparisons  
**Expected DSL**: `{ "days": $count(results.forecast), "highest_temp": $max(results.forecast.max), "lowest_temp": $min(results.forecast.min), "avg_humidity": $average(results.forecast.humidity) }`  
**Expected Output**:
```json
{
  "days": 2,
  "highest_temp": 25,
  "lowest_temp": 13,
  "avg_humidity": 58.5
}
```
**Validation**:
- ✓ Count of array elements
- ✓ Nested field max/min
- ✓ Average of nested field

---

### TC-008: First Day Detailed Report
**Goal**: Create detailed report for first forecast day with string concatenation  
**Difficulty**: ⭐⭐⭐ Hard  
**Tests**: Array indexing, string concatenation  
**Expected DSL**: `results.forecast[0].{ "date": full_date, "weekday": weekday, "condition": description, "temp_range": (max & "°C to " & min & "°C"), "humidity": (humidity & "%") }`  
**Expected Output**:
```json
{
  "date": "05/01/2026",
  "weekday": "Seg",
  "condition": "Chuvas esparsas",
  "temp_range": "25°C to 13°C",
  "humidity": "53%"
}
```
**Validation**:
- ✓ Array index notation [0]
- ✓ String concatenation with & operator
- ✓ Nested field access from indexed element

---

## Test Execution Scripts

### Run All Real-World Tests
```powershell
.\tests\preview-transform-real-tests.ps1 -ApiUrl "http://localhost:5000" -Verbose $true
```

### Run Integration Tests with Connector/Process Lifecycle
```powershell
.\tests\preview-transform-integration-real-tests.ps1 -ApiUrl "http://localhost:5000"
```

---

## Success Criteria

| Metric | Target | Acceptance |
|--------|--------|-----------|
| TC-001 to TC-002 Pass Rate | 100% | ≥ 90% |
| TC-003 Pass (Critical) | ✓ Pass | MUST PASS |
| TC-004 to TC-007 Pass Rate | 100% | ≥ 85% |
| TC-008 Pass | ✓ Pass | ≥ 80% |
| **Overall Success Rate** | **100%** | **≥ 80%** |

---

## Robustness Validation Checklist

- [ ] Path analysis correctly identifies nested structures
- [ ] Array iteration uses correct paths (e.g., `results.forecast`)
- [ ] Arithmetic on collections uses proper parenthesization
- [ ] Aggregation functions work with array fields
- [ ] String concatenation uses & operator
- [ ] Object construction maintains field names
- [ ] No false positives in error detection
- [ ] LLM respects sample input structure
- [ ] DSL syntax follows Jsonata rules
- [ ] Preview output validates correctly

---

## Notes

**Real API Characteristics**:
- Response is deeply nested (results → forecast → array of objects)
- Mixed data types (strings, numbers, arrays, objects)
- Variable field names (snake_case, camelCase)
- Multiple levels of nesting test path analysis
- Aggregation functions test complex expressions

**DSL Generation Improvements Being Validated**:
1. System prompt now includes structure analysis instruction
2. User prompt includes automated path mapping from sample input
3. LLM instructed to analyze and verify paths before generation
4. Arithmetic expressions with double parentheses in aggregations

**Expected Improvements After Prompt Enhancement**:
- TC-003 should pass consistently (was failing before with path errors)
- TC-005, TC-007 should show improvement (aggregations with correct paths)
- Fewer repair attempts needed
- Higher success on first generation attempt

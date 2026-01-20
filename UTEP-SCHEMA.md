# UTEP CLI v1.0 (JSON + Dependencies + Doctor)

## 0) Главные принципы (обновлённые)

1. **Истина в JSON-файлах** (`goal.json`, `*.task.json`, `utep.log.ndjson`).
2. **View для человека** (`index.md`) генерируется CLI.
3. **Агент/человек не редактируют файлы вручную** (только CLI).
4. `utep next` возвращает **только выполнимое сейчас** (`Actionable`).
5. Любая валидация должна иметь путь “разрулить”, чтобы работа не встала:

   * `utep validate` → сообщает
   * `utep doctor` → предлагает/делает фикс, или переводит в degraded mode
   * `utep diagnose` → единая точка входа (`validate` или `doctor --fix`)
6. Зависимости **без политик**: `blocked_by: string[]`.

   * “что делать, если блокер отменили” решает актуальность + auto `needs_review`.

---

# 1) Структура репозитория

```
/unlimotion/
  utep.config.json
  /.utep/
    context.json
  /goals/
    G-2026-001/
      goal.json
      index.md
      /tasks/
        T-001.task.json
        T-002.task.json
      /logs/
        utep.log.ndjson
      /artifacts/
```

---

# 2) Форматы файлов (контракт)

## 2.1 `utep.config.json`

```json
{
  "version": 1,
  "limits": {
    "attempt_limit": 3,
    "time_limit_minutes": 90,
    "large_task_minutes": 240
  },
  "thresholds": {
    "confidence_min": 0.7
  },
  "render": {
    "index": true,
    "index_filename": "index.md"
  },
  "output": {
    "default": "human"
  }
}
```

## 2.2 `goal.json`

```json
{
  "version": 1,
  "goal": {
    "id": "G-2026-001",
    "title": "Сделать UTEP CLI v1",
    "status": "Planned",
    "created_at": "2026-01-15T01:00:00-05:00",
    "updated_at": "2026-01-15T01:00:00-05:00",
    "success_criteria": [
      "CLI спецификация реализована",
      "Index.md рендерится и отражает дерево и зависимости"
    ],
    "next_task_id": null
  },
  "meta": {
    "owner": "human",
    "tags": ["utep", "cli"]
  }
}
```

`goal.status` обновляется CLI на основе состояния задач.

## 2.3 `T-xxx.task.json`

```json
{
  "version": 1,
  "task": {
    "id": "T-010",
    "goal_id": "G-2026-001",
    "parent_id": "T-002",
    "title": "Настроить CI",
    "status": "Ready",
    "priority": 2,
    "risk": "Med",
    "cost_estimate_minutes": 60,
    "success_criteria": [
      "Pipeline запускается на PR",
      "Есть e2e шаги"
    ],
    "confidence": 0.8,
    "dependencies": {
      "blocked_by": ["T-005", "T-007"]
    },
    "assumptions": [
      {"id": "A-01", "text": "GitLab доступен"}
    ],
    "open_questions": [],
    "attempts": 0,
    "time_spent_minutes": 0,
    "active_attempt_started_at": null,
    "evidence": []
  },
  "links": {
    "artifacts_dir": "../artifacts/"
  }
}
```

`active_attempt_started_at` используется для авто‑расчета `time_spent_minutes`, если `--minutes` не указан.

### `open_questions[]` (структурный объект)

```json
{
  "id": "Q-01",
  "kind": "architectural",
  "question": "Выбрать формат вывода по умолчанию?",
  "options": [
    {"id": "O-1", "title": "JSON default", "pros": ["удобно агенту"], "cons": ["хуже человеку"], "risks": []},
    {"id": "O-2", "title": "Human default + --json", "pros": ["удобно человеку"], "cons": ["агенту помнить флаг"], "risks": []}
  ],
  "recommendation": "O-2",
  "requested_answer": "Выберите O-1 или O-2",
  "answer": null,
  "created_at": "2026-01-15T01:10:00-05:00"
}
```

`answer` опционален: хранит выбранный `option.id` или текстовый ответ.

## 2.4 `utep.log.ndjson` (события)

Одна строка — один JSON:

```json
{"at":"2026-01-15T01:12:00-05:00","actor":"cli","event":"task.status_changed","goal_id":"G-2026-001","task_id":"T-010","from":"Planned","to":"Ready","note":"manual"}
```

---

# 3) Вычисляемые состояния (CLI output-only)

Файлы хранят “сырое”, CLI вычисляет:

* `is_unblocked`: все `blocked_by` в терминальном статусе
* `blocked_by`: список зависимостей, которые ещё не терминальны
* `needs_review`: true если любой блокер терминален, но **не Completed**
  (Cancelled/Invalidated) → значит зависимая задача может требовать переоценки успех-критериев
* `effective_state`:

  * `Actionable` (Ready + unblocked)
  * `Blocked` (Ready, но blocked_by не закрыты)
* `Question` (status Question и есть open_questions)
  * `Terminal` (Completed/Cancelled/Invalidated)
  * `NotReady` (Draft/Planned)

**Ключ:** “Blocked” — не статус в файле, а вычисляемый флаг.

---

# 4) Обновлённые команды CLI

## 4.1 `utep next` — только actionable

### `utep next [--count N] [--json]`

Возвращает список задач (до N), которые:

* `status == Ready`
* не терминальные
* `is_unblocked == true`

Если список пуст:

* exit code `5`
* возвращает `reason` без списка блокеров:
  * `question` если есть `Question` с вопросами
  * `blocked` если есть блокировки
  * `none` если нет ни actionable, ни блокировок

**Пример `--json` ответа, когда пусто:**

```json
{
  "goal_id": "G-2026-001",
  "actionable": [],
  "reason": "blocked"
}
```

## 4.2 Новые команды под зависимости/параллельность

* `utep bottlenecks [--top N] [--json]`
  Показывает задачи, которые блокируют больше всего других (по `blocked_by`).

* `utep task dep add <task_id> --blocked-by <id>`

* `utep task dep rm <task_id> --blocked-by <id>`

* `utep task deps <task_id>` (show blockers + blocked)

---

# 5) Validate vs Doctor (чтобы работа не вставала)

## 5.1 `utep validate [--goal ...]`

* только диагностика (ошибки/предупреждения)
* exit code 0 если ok, 2 если есть ошибки

## 5.2 `utep doctor [--goal ...] [--fix] [--json]`

* для каждой ошибки:

  * объясняет причину
  * даёт список “remedies”
  * если `--fix` и remedy автоматизируем — применяет
* если есть неустранимые автоматически — предлагает пошаговый план

## 5.3 `utep diagnose [--goal ...] [--fix] [--json]`

* без `--fix` ведет себя как `utep validate`
* с `--fix` ведет себя как `utep doctor --fix`

### Типы ошибок и remedies (минимум)

1. `E001 MissingTaskFile` (в ссылках встречается task_id, но файла нет)
   Remedies:

   * удалить ссылку (dep rm)
   * создать заглушку `Draft` task (task new --id)
2. `E002 OrphanParent` (parent_id не существует)
   Remedies:

   * parent_id = null
   * создать родителя
3. `E003 DependencyCycle`
   Remedies:

   * показать цикл
   * предложить разорвать одну дугу (dep rm)
4. `E004 CompletedWithoutEvidence`
   Remedies:

   * добавить evidence-template
5. `E008 QuestionWithoutOpenQuestions`
   Remedies:

   * создать шаблон вопроса и импортировать
6. `E006 MissingSuccessCriteria`
   Remedies:

   * добавить success_criteria через CLI (перед переводом в Ready/InProgress/Completed)
7. `E007 MissingBlockedByList`
   Remedies:

   * явно задать пустой список blocked_by
8. `E012 MissingActiveAttemptSession`
   Remedies:

   * `utep task start <id>`
   * `utep task attempt <id> --minutes N --evidence "..."`
9. `E009 InvalidStatusTransitionDetectedInLog` (если используешь восстановление)
   Remedies:

   * “replay from snapshot” / “ignore log”
   * (для MVP можно не включать)

## 5.3 Degraded mode (очень важно)

Если репо частично невалидно:

* `utep next` **не падает**, а выбирает среди валидных задач
* `utep tree/status/render` пытаются показать максимум, помечая “⚠ inconsistent”

---

# 6) Алгоритм выбора next с зависимостями (строго)

### Кандидаты Actionable:

* status == Ready
* is_unblocked == true
* не терминальные

Сортировка:

1. depth ↑ (ближе к корню)
2. `blocks_count` ↓ (делаем то, что открывает больше задач)
3. priority ↑
4. created_at ↑

`utep next --count N` возвращает N лучших.

---

# 7) Обновление runbook агента (только через CLI)

**Ключевое изменение:** агент запрашивает пул и, если пул пуст, переключается на bottlenecks или ждёт ответа пользователя.

```markdown
## Main loop
1) `utep next --count 5 --json`
   - if actionable not empty -> pick first
   - else:
       - if reason == "question": run `utep goal tree --json`, find task with `effective_state == "Question"`,
         then `utep task show <id> --json` and ask the user
       - if reason == "blocked": run `utep bottlenecks --top 5 --json`,
         pick the best blocker task (if any is actionable) and work on it
       - if reason == "none": stop and report

2) `utep task show <id> --json`
3) Relevance check -> invalidate if needed
4) `utep task start <id>`
5) work + `utep task attempt ...`
6) complete or block with options via CLI
7) `index.md` обновляется автоматически; при необходимости `utep render` или `utep report`, затем `utep diagnose`
```

---

# 8) Рендер index.md: как показывать зависимости, не засоряя

В дереве оставляем только короткий маркер:

* `⛔ blocked: T-005,T-007` если `Blocked`
* `⚠ review` если `needs_review == true`
* `🟥 Question` если есть open_questions

Плюс отдельный раздел:

* **Bottlenecks** (топ блокеров)
* **Blocked** (несколько примеров “что на что ждёт”)

---

# 9) Что добавить в CLI “для удобства жизни”

### 9.1 Атомарные записи (transactional writes)

Каждая команда:

* пишет в temp файл
* fsync (по возможности)
* rename поверх (atomic)
  Чтобы репо не ломалось при падениях.

### 9.2 Канонический JSON

CLI всегда пишет:

* стабильный порядок ключей
* 2 пробела
* \n в конце

Это делает диффы чистыми.

---

# 10) Итого: что изменилось по сравнению с прошлой версией

* YAML → JSON
* зависимости без политик: `blocked_by: string[]`
* `Blocked` и `needs_review` вычисляются, не хранятся как статус
* `next` никогда не выдаёт невыполнимое, но объясняет “почему пусто”
* `doctor` для разруливания всех типов валидаций + degraded mode



Ниже — **строгие output schemas** для UTEP CLI v1.1. Я дам:

* единый “envelope” (общие поля для всех команд),
* типы (TaskRef, ValidationIssue, NextResponse…),
* JSON-структуры по каждой команде,
* коды ошибок и что именно возвращать при ошибках,
* правила стабильности (ключевой порядок/версии).

Я буду использовать “JSON Schema–подобное” описание (типами и обязательностью), чтобы было легко реализовать без полного draft-07.

---

# 0) Общие правила вывода

## 0.1 Режимы

* `--json` → **строго JSON**, один объект в stdout, без лишнего текста.
* без `--json` → human (stdout), но при ошибке всё равно можно включать `--json`.

## 0.2 Единый envelope для всех JSON-ответов

```json
{
  "utep_version": "1.1",
  "command": "utep next",
  "repo_root": "/abs/path",
  "goal_id": "G-2026-001",
  "ok": true,
  "result": { ... },
  "warnings": [],
  "errors": [],
  "meta": {
    "timestamp": "2026-01-15T01:30:00-05:00",
    "duration_ms": 37
  }
}
```

### Поля

* `utep_version` (string, required)
* `command` (string, required)
* `repo_root` (string, required)
* `goal_id` (string|null, required)
* `ok` (bool, required)
* `result` (object|null, required)
* `warnings` (ValidationIssue[], required, can be empty)
* `errors` (ValidationIssue[], required, can be empty)
* `meta` (object required: timestamp, duration_ms)

## 0.3 ValidationIssue (универсальный формат warning/error)

```json
{
  "code": "E003",
  "severity": "error",
  "message": "Dependency cycle detected",
  "details": {
    "cycle": ["T-010", "T-012", "T-010"]
  },
  "locations": [
    {"kind": "task", "id": "T-010", "path": "goals/.../tasks/T-010.task.json"}
  ],
  "remedies": [
    {
      "id": "R1",
      "title": "Remove dependency T-012 from T-010",
      "commands": ["utep task dep rm T-010 --blocked-by T-012"]
    }
  ]
}
```

### Поля

* `code` (string, required) e.g. `E001`, `W101`
* `severity` (`warning|error`, required)
* `message` (string, required)
* `details` (object, optional)
* `locations` (Location[], optional)
* `remedies` (Remedy[], optional)

### Location

* `kind`: `goal|task|file|config|log`
* `id`: string|null
* `path`: string|null
* `json_pointer`: string|null (optional) e.g. `/task/dependencies/blocked_by/0`

### Remedy

* `id` string
* `title` string
* `commands` string[] (CLI commands user/agent can run)

---

# 1) Базовые типы (используются в разных командах)

## 1.1 TaskStatus

`Draft|Planned|Ready|InProgress|Question|Completed|Cancelled|Invalidated`

## 1.2 TaskRef (краткая ссылка)

```json
{
  "task_id": "T-010",
  "title": "Настроить CI",
  "status": "Ready",
  "priority": 2,
  "depth": 1,
  "file": "goals/G-.../tasks/T-010.task.json"
}
```

## 1.3 EffectiveState (вычисляемое)

`Actionable|Blocked|Question|Terminal|NotReady`

## 1.4 TaskComputed (вычисляемые поля)

```json
{
  "effective_state": "Blocked",
  "is_unblocked": false,
  "blocked_by": ["T-005"],
  "needs_review": false,
  "blocks_count": 6
}
```

## 1.5 GoalSummary

```json
{
  "goal_id": "G-2026-001",
  "title": "Сделать UTEP CLI v1",
  "status": "Planned",
  "counts": {
    "Draft": 0, "Planned": 2, "Ready": 1, "InProgress": 0,
    "Question": 1, "Completed": 3, "Cancelled": 0, "Invalidated": 0
  },
  "next_task_id": "T-003",
  "repo_path": "goals/G-2026-001/"
}
```

---

# 2) Output schemas по командам

## 2.1 `utep init`

**result:**

```json
{
  "created": [
    "utep.config.json",
    "goals/",
    ".utep/context.json"
  ],
  "repo_root": "/abs/path"
}
```

---

## 2.2 `utep goal new --title ...`

**result:**

```json
{
  "goal": {
    "goal_id": "G-2026-001",
    "title": "Сделать UTEP CLI v1",
    "status": "Planned",
    "file": "goals/G-2026-001/goal.json",
    "index_file": "goals/G-2026-001/index.md"
  }
}
```

---

## 2.3 `utep goal open <goal_id>`

**result:**

```json
{
  "goal_id": "G-2026-001",
  "context_file": ".utep/context.json"
}
```

---

## 2.4 `utep goal status [goal_id]`

**result:** `GoalSummary`

---

## 2.5 `utep goal tree [goal_id]`

**result:**

```json
{
  "goal": { "goal_id": "G-2026-001", "title": "..." },
  "nodes": [
    {
      "task": { "...TaskRef..." },
      "computed": { "...TaskComputed..." },
      "children": ["T-002", "T-003"]
    }
  ],
  "roots": ["T-001", "T-004"]
}
```

Примечание: `nodes` — массив, а не dict, чтобы вывод был стабильный. Стабильная сортировка по `depth, priority, created_at`.

---

## 2.6 `utep task new ...`

**result:**

```json
{
  "task": {
    "task_id": "T-011",
    "title": "Сделать validate",
    "status": "Planned",
    "parent_id": "T-002",
    "goal_id": "G-2026-001",
    "file": "goals/G-.../tasks/T-011.task.json"
  }
}
```

---

## 2.7 `utep task show <task_id>`

**result:**

```json
{
  "task": {
    "version": 1,
    "task": {
      "id": "T-010",
      "goal_id": "G-2026-001",
      "parent_id": "T-002",
      "title": "Настроить CI",
      "status": "Ready",
      "priority": 2,
      "risk": "Med",
      "cost_estimate_minutes": 60,
      "success_criteria": ["..."],
      "confidence": 0.8,
      "dependencies": { "blocked_by": ["T-005"] },
      "assumptions": [{"id":"A-01","text":"..."}],
      "open_questions": [],
      "attempts": 1,
      "time_spent_minutes": 30,
      "active_attempt_started_at": null,
      "evidence": [{"kind":"note","text":"...","at":"..."}]
    },
    "links": { "artifacts_dir": "../artifacts/" }
  },
  "computed": {
    "effective_state": "Blocked",
    "is_unblocked": false,
    "blocked_by": ["T-005"],
    "needs_review": false,
    "blocks_count": 6
  },
  "relations": {
    "children": ["T-010.1"],
    "parent": "T-002",
    "blocks": ["T-012", "T-013"]
  }
}
```

---

## 2.8 `utep task set-status <id> <status> [--note]`

**result:**

```json
{
  "task_id": "T-010",
  "from": "Planned",
  "to": "Ready",
  "note": "validated",
  "log_event_id": "evt-000123",
  "rendered": true
}
```

Если переход невозможен → `ok:false`, `errors:[{code:"E400", ...}]`, exit code 4.

---

## 2.9 `utep task start <id>`

**result:**

```json
{
  "task_id": "T-010",
  "from": "Ready",
  "to": "InProgress",
  "attempt_session": {
    "started_at": "2026-01-15T01:40:00-05:00",
    "attempts_before": 1
  },
  "rendered": true
}
```

Если задача не actionable (waiting deps) → `ok:false`, `errors` с `code:"E410"` и `details.blocked_by`.

---

## 2.10 `utep task attempt <id> --evidence ... [--minutes N] [--evidence-file] [--note]`

Если `--minutes` не указан, CLI рассчитывает длительность по `active_attempt_started_at`.

**result:**

```json
{
  "task_id": "T-010",
  "attempts": { "before": 1, "after": 2 },
  "time_spent_minutes": { "before": 30, "after": 60 },
  "evidence_added": [
    {"kind":"note","text":"What was done","at":"..."}
  ],
  "rendered": false
}
```

---

## 2.11 `utep task complete <id> --evidence ... [--evidence-file] [--minutes N]`

Опционально поддерживается `--minutes N`. Если `--minutes` не указан, CLI использует `active_attempt_started_at`.

**result:**

```json
{
  "task_id": "T-010",
  "from": "InProgress",
  "to": "Completed",
  "evidence_added": [{"kind":"completion","text":"...","at":"..."}],
  "parent_check": {
    "ran": true,
    "affected_tasks": [
      {"task_id":"T-002","from":"Planned","to":"Ready","reason":"all children terminal"}
    ]
  },
  "rendered": true
}
```

Если не выполнены минимальные требования:
* нет success_criteria → `E006`
* нет evidence → `E004`

---

## 2.12 `utep task invalidate|cancel <id> --reason ...`

**result:**

```json
{
  "task_id": "T-007",
  "from": "Ready",
  "to": "Invalidated",
  "reason": "Context changed",
  "rendered": true
}
```

---

## 2.13 `utep task block <id> --question-file ...`

**result:**

```json
{
  "task_id": "T-010",
  "from": "InProgress",
  "to": "Question",
  "question_imported": {
    "file": "questions/T-010.md",
    "open_question_id": "Q-02",
    "kind": "architectural"
  },
  "rendered": true
}
```

Если файл невалиден → `E430` (details: parse errors).

---

## 2.13a `utep task question <id> --kind ... --question ... --requested-answer ... [--option <id:title>] [--recommendation <id>]`

**result:**

```json
{
  "task_id": "T-010",
  "from": "InProgress",
  "to": "Question",
  "open_question_id": "Q-01",
  "rendered": true
}
```

Формат `--option`: `O-1:Краткий заголовок`.

---

## 2.13b `utep task answer <id> --option <O-1>`

**result:**

```json
{
  "task_id": "T-010",
  "open_question_id": "Q-01",
  "answer": "O-1",
  "rendered": false
}
```

Для вопросов без опций использовать `--text`.
Статус задачи не меняется автоматически.

---

## 2.14 `utep task dep add|rm <id> --blocked-by <id>`

**result:**

```json
{
  "task_id": "T-010",
  "change": "added",
  "blocked_by": ["T-005","T-007"],
  "rendered": true
}
```

Если создаёт цикл → `ok:false`, `errors:[E003]`, exit code 2 (validation).

---

## 2.15 `utep next [--count N]`

### Если есть actionable

**result:**

```json
{
  "actionable": [
    {
      "task": { "...TaskRef..." },
      "computed": { "...TaskComputed..." },
      "selection_reason": {
        "depth": 1,
        "blocks_count": 6,
        "priority": 2,
        "rule": "depth, blocks_count, priority, created_at"
      }
    }
  ],
  "reason": null
}
```

### Если actionable нет

**result:**

```json
{
  "actionable": [],
  "reason": "question"
}
```

Exit code: `5`.

---

## 2.16 `utep bottlenecks --top N`

**result:**

```json
{
  "top": [
    {
      "task": { "...TaskRef..." },
      "blocks_count": 12,
      "blocked_tasks_sample": ["T-010","T-011","T-012"]
    }
  ]
}
```

---

## 2.17 `utep validate`

**result:**

```json
{
  "summary": {
    "errors": 1,
    "warnings": 2
  },
  "issues": [ { "...ValidationIssue..." } ]
}
```

---

## 2.18 `utep doctor [--fix]`

**result:**

```json
{
  "summary": {
    "errors_before": 3,
    "errors_after": 1,
    "fixed": 2,
    "requires_manual": 1
  },
  "actions": [
    {
      "issue_code": "E002",
      "remedy_id": "R1",
      "applied": true,
      "commands_executed": ["utep task edit T-010 --set parent_id=null"]
    }
  ],
  "remaining_issues": [ { "...ValidationIssue..." } ]
}
```

Терминальные задачи (`Completed`, `Cancelled`, `Invalidated`) в выборку bottlenecks не попадают.

---

## 2.18a `utep diagnose [--fix]`

* без `--fix` возвращает `ValidateResult`
* с `--fix` возвращает `DoctorResult`

---

## 2.19 `utep render`

**result:**

```json
{
  "rendered": true,
  "files": [
    "goals/G-2026-001/index.md"
  ]
}
```

---

## 2.20 `utep report`

**result:**

```json
{
  "rendered": true,
  "files": [
    "goals/G-2026-001/report.md"
  ]
}
```

---

# 3) Ошибки выполнения (execution errors) и их JSON

Все “ошибки” возвращаются в envelope:

* `ok:false`
* `result:null` или частичный
* `errors:[ValidationIssue...]`

### Каталог ключевых execution-кодов

* `E400 InvalidTransition` (exit 4)
* `E410 NotActionable` (trying start when waiting deps/user) (exit 4)
* `E005 MissingEvidence` (exit 2)
* `E006 MissingSuccessCriteria` (exit 2)
* `E430 QuestionParseError` (exit 2)
* `E431 InvalidQuestionAnswer` (exit 2)
* `E012 MissingActiveAttemptSession` (exit 2)
* `E440 NotFound` (task/goal) (exit 3)
* `E450 RepoNotInitialized` (exit 3)

Пример `E410`:

```json
{
  "ok": false,
  "errors": [{
    "code": "E410",
    "severity": "error",
    "message": "Task is not actionable due to dependencies",
    "details": {"blocked_by":["T-005"]},
    "remedies": [{
      "id":"R1",
      "title":"Work on the blocker",
      "commands":["utep task show T-005 --json","utep next --json"]
    }]
  }]
}
```

---

# 4) Требования к стабильности (чтобы агент не ломался)

1. `utep_version` неизменен в рамках мажора.
2. Все команды возвращают envelope.
3. Поля в `result` добавляются только назад-совместимо.
4. ID и file paths стабильны.
5. Массивы сортируются детерминированно:

   * actionable: по правилам выбора
   * bottlenecks: blocks_count desc, затем depth asc, затем created_at
6. Время всегда ISO-8601 с timezone.

---

# 5) Минимальные JSON schemas файлов questions (для `task block`)

Чтобы `--question-file` был формальным, лучше принимать **JSON** (и отдельно позволить MD как “display-only”, но тогда CLI не сможет вернуть options структурно).

### `questions/T-010.question.json`

```json
{
  "kind": "architectural",
  "question": "Какой формат вывода по умолчанию?",
  "options": [
    {"id":"O-1","title":"JSON default","pros":["..."],"cons":["..."],"risks":[]},
    {"id":"O-2","title":"Human default + --json","pros":["..."],"cons":["..."],"risks":[]}
  ],
  "recommendation": "O-2",
  "requested_answer": "Выберите O-1 или O-2"
}
```

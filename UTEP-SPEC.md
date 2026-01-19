# Unlimotion Task Execution Protocol (UTEP) v1.1

---

## 1. Назначение

UTEP — это протокол и CLI-утилита для **планирования, исполнения и контроля целей ИИ-агентом** в условиях:

* неопределённости,
* параллельного выполнения,
* зависимостей между задачами,
* обязательной валидации и актуализации.

UTEP **не является таск-менеджером**.
Это **исполняемый протокол принятия решений**.

---

## 2. Главные принципы (v1.1)

1. **Истина в JSON-файлах** (`goal.json`, `*.task.json`, `utep.log.ndjson`).
2. **CLI — единственный исполнитель правил.**
3. **Агент и человек не редактируют файлы напрямую.**
4. **`next` возвращает только выполнимое сейчас (Actionable).**
5. **Любая ошибка валидации должна иметь путь разрешения** (`doctor`).
6. **Дерево задач — для смысла, зависимости — для порядка.**
7. **View (`index.md`) генерируется CLI.**
8. **Degraded mode:** `next`/`render` работают даже при частичной невалидности.

---

## 3. Структура репозитория

```
/utep/
  utep.config.json
  /.utep/
    context.json
  /goals/
    G-2026-001/
      goal.json
      index.md
      /tasks/
        T-001.task.json
      /logs/
        utep.log.ndjson
      /artifacts/
```

---

## 4. Модель данных (v1.1)

### 4.1 utep.config.json

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

### 4.2 goal.json

```json
{
  "version": 1,
  "goal": {
    "id": "G-2026-001",
    "title": "Сделать UTEP CLI",
    "status": "Planned",
    "success_criteria": ["..."],
    "created_at": "...",
    "updated_at": "...",
    "next_task_id": null
  },
  "meta": {
    "owner": "human",
    "tags": ["utep"]
  }
}
```

### 4.3 task.json

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
    "success_criteria": ["..."],
    "confidence": 0.8,
    "dependencies": { "blocked_by": ["T-005"] },
    "assumptions": [],
    "open_questions": [],
    "attempts": 0,
    "time_spent_minutes": 0,
    "evidence": []
  },
  "links": { "artifacts_dir": "../artifacts/" }
}
```

### 4.4 open_questions[]

```json
{
  "id": "Q-01",
  "kind": "architectural",
  "question": "...",
  "options": [{"id":"O-1","title":"...","pros":[],"cons":[],"risks":[]}],
  "recommendation": "O-2",
  "requested_answer": "...",
  "answer": null,
  "created_at": "..."
}
```

`answer` опционален и хранит выбранный `option.id` или текст ответа.
Ответ не переводит задачу из статуса `Question` автоматически.

### 4.5 utep.log.ndjson

Каждая строка — JSON-событие.

```json
{"at":"2026-01-15T01:12:00-05:00","actor":"cli","event":"task.status_changed","goal_id":"G-2026-001","task_id":"T-010","from":"Planned","to":"Ready","note":"manual"}
```

---

## 5. Статусы задач

```
Draft
Planned
Ready
InProgress
Question
Completed
Cancelled
Invalidated
```

**Терминальные:** `Completed`, `Cancelled`, `Invalidated`.

---

## 6. Вычисляемые состояния (не хранятся)

CLI вычисляет:

| Состояние           | Смысл                               |
| ------------------- | ----------------------------------- |
| Actionable          | Ready + зависимости сняты           |
| Blocked | Ready, но ждёт блокеры              |
| Question         | Question с вопросом                  |
| NotReady            | Draft / Planned                     |
| Terminal            | Completed / Cancelled / Invalidated |

---

## 7. Алгоритм выбора задач (`next`)

**Кандидаты:**

* `status == Ready`
* зависимости сняты
* не терминальные

**Сортировка:**

1. depth ↑
2. blocks_count ↓
3. priority ↑
4. created_at ↑ (если есть) / стабильный tie‑break

**Если кандидатов нет:**

* `reason == "question"` → агент должен найти вопрос через дерево цели
* `reason == "blocked"` → агент работает с блокерами
* `reason == "none"` → остановиться
* exit code `5`

**После получения ответа:** агент обязан проверить, уменьшает ли ответ неопределённость и стало ли понятно, что делать дальше. Если нет — сформировать уточняющие вопросы.

---

## 8. JSON‑envelope для всех команд (`--json`)

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

Ошибки и предупреждения используют `ValidationIssue` из `UTEP-SCHEMA.md`.

---

## 9. Команды CLI (v1.1)

* `utep init`
* `utep goal new|open|status|tree`
* `utep task new|show|set-status|start|attempt|complete|invalidate|cancel|block|question|answer`
* `utep task dep add|rm`
* `utep task deps`
* `utep next`
* `utep bottlenecks`
* `utep validate`
* `utep doctor [--fix]`
* `utep diagnose [--fix]`
* `utep render`

Все JSON‑форматы команд соответствуют `UTEP-SCHEMA.md`.

---

## 10. Валидация и восстановление

### `utep validate`

* только диагностика
* exit code `2`, если есть ошибки

### `utep doctor`

* объясняет проблему
* предлагает remedies
* может исправлять автоматически (`--fix`)

**Минимальные ошибки:** E001–E009 (см. `UTEP-SCHEMA.md`).

### `utep diagnose`

* единая команда: без `--fix` = `validate`, с `--fix` = `doctor --fix`

---

## 11. View (index.md)

* генерируется CLI
* дерево задач + маркеры:
  * `⛔ deps: ...`
  * `⚠ review`
  * `🟥 Question`
* отдельные секции `Bottlenecks` и `Blocked`

---

## 12. Гарантии

* JSON пишется канонически (стабильные ключи, 2 пробела, \n в конце)
* записи атомарные (temp + rename)
* CLI не оставляет репозиторий в полусостоянии
* агент не может нарушить правила протокола

---

## 13. Источник истины

Детальная спецификация форматов и ответов находится в `UTEP-SCHEMA.md`.

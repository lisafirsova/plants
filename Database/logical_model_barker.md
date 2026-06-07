# Логическая модель БД в нотации Баркера

## Сущности

**PLANT**
- # plant_id
- * name
- latin_name
- type
- location
- last_watered
- watering_hint
- health_status
- notes
- accent_color

**PLANT_PHOTO**
- # photo_id
- * plant_id
- * caption
- * taken_at
- placeholder_color

**CARE_TASK**
- # task_id
- * plant_id
- * title
- description
- * due_date
- * status

**PEST_ISSUE**
- # issue_id
- * name
- * issue_type
- severity
- symptoms
- treatment
- prevention
- accent_color

## Связи

- PLANT может иметь ноль, одну или много PLANT_PHOTO.
- PLANT_PHOTO обязательно относится к одному PLANT.
- PLANT может иметь ноль, одну или много CARE_TASK.
- CARE_TASK обязательно относится к одному PLANT.
- PEST_ISSUE является справочной сущностью и хранит вредителей и болезни.

## Физическое представление

Минимальная физическая таблица, которая уже используется на главной странице приложения, описана в `schema_postgresql.sql`: `plants`.

Для полной модели можно добавить таблицы `plant_photos`, `care_tasks`, `pest_issues` с внешними ключами `plant_id -> plants.id`.

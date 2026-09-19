# 15 — Create asset reference library

## Цель

Создать локальную библиотеку бесплатных внешних 3D-ассетов для исследования и
последующего выбора, не включаемую в поставку игры.

## Границы

- Скачать только бесплатные Standard-версии выбранных Quaternius-паков с CC0.
- Исключить `.asset-library/` из Git и поставки.
- Зафиксировать provenance, лицензию и порядок поиска для будущих фич.
- Не интегрировать модели, не менять Unity/runtime и не скачивать платные
  Pro/Source-варианты.

## Проверка

- Каталог отсутствует в `git status`.
- `ASSET_LIBRARY.md` описывает search-first и approval-before-authoring policy.
- Для каждого скачанного пакета есть `SOURCE.md` с URL и CC0 provenance.

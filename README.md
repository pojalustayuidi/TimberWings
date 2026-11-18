# TimberWings

Одиночное приключение-симулятор лесоруба на парящем острове. Лео добывает древесину, развивает лагерь, знакомится с жителями, исследует тайны леса и строит мост к порталу. Глава I — Лесоруб; будущая глава II — Шахтёр.

**Статус:** стабилизация главы I перед Simulator Update. Игра в целом проходилась в Unity, но standalone-билд не принят: известны блокеры карты и финала. Стабильный релиз ещё не зафиксирован.

**Исходный срез:** `main` @ [`9afd413`](https://github.com/pojalustayuidi/TimberWings/tree/9afd4132335022fd5c13dff2dceea560c4ef381e), 18.11.2026. Unity 6000.3.10f1, C#, URP. Аудит исходников не заменяет запуск и проверку сборки.

## Документация

| Документ | Назначение |
|---|---|
| [docs/README.md](docs/README.md) | Навигатор и правила актуальности |
| [GDD.md](docs/GDD.md) | Действующая игра: мир, сюжет, механики |
| [BALANCE.md](docs/BALANCE.md) | Текущие числа, формулы, экономика |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Системы, классы, технические правила |
| [BUILD_QA.md](docs/BUILD_QA.md) | Запуск, сборка и тестирование |
| [ROADMAP.md](docs/ROADMAP.md) | Единая очередь крупных задач |
| [SIMULATOR_UPDATE.md](docs/SIMULATOR_UPDATE.md) | Будущая обнова — черновой дизайн |
| [CHANGELOG.md](docs/CHANGELOG.md) | Принятые крупные изменения |
| [WORKFLOW.md](docs/WORKFLOW.md) | Ветки, обновление документов, шаблон задач |

## Запуск проекта

1. Установить Unity Hub и Unity 6000.3.10f1 с Windows Build Support.
2. Установить Git LFS; `git lfs install`, `git clone https://github.com/pojalustayuidi/TimberWings.git`, `cd TimberWings`, `git lfs pull`.
3. Открыть корень проекта через Unity Hub, дождаться импорта и успешной компиляции.
4. Открыть `Assets/_Project/Scenes/MainMenu.unity`; для отладки доступна `MainLevel.unity`. Порядок сцен нужно сверить в Build Profiles.
5. Перед релизом пройти [BUILD_QA](docs/BUILD_QA.md).

Лицензия: [LICENSE](LICENSE). Условия использования сторонних ассетов проверяются отдельно.

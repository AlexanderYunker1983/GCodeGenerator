<!--
Правила проекта — в CONTRIBUTING.md. Ниже то, что проверяют в первую очередь.
Project rules are in CONTRIBUTING.md.
-->

## Что изменилось и почему

<!-- Что было не так, почему это важно и что теперь по-другому. -->

## Проверено

- [ ] `dotnet restore GCodeGenerator.sln --locked-mode` — замки актуальны
- [ ] `dotnet build GCodeGenerator.sln -c Release` — без предупреждений
- [ ] `dotnet test GCodeGenerator.sln -c Release` — все тесты зелёные
- [ ] Новая проверка прогнана на неисправленном коде и падает
- [ ] Эталонные файлы обновлены осознанно: разница — только то, что менялось
- [ ] Заметное для пользователя изменение попало в `CHANGELOG.md`

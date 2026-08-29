# Внешние CAD-фикстуры

Эти файлы не создаются тестами и не проходят через writer `netDxf`. Они взяты
из официального репозитория LibreCAD на закреплённом commit
`a05b4261f61d61e23d8c27c8d869ede756c2ac2b` и проверяют совместимость с DXF,
которые выпущены независимым CAD-приложением.

| Локальный файл | Исходный путь в LibreCAD | SHA-256 | Особенность |
| --- | --- | --- | --- |
| `librecad-square.dxf` | `librecad/support/patterns/square.dxf` | `69031DD7A93E9D429EE6EFBDEB3AD49CCC6CB73A1ACF95D16F0574B4C075DD80` | legacy-заголовок без `ACADVER`, четыре `LINE`, `INSUNITS=0` |
| `librecad-block4-lwpolyline.dxf` | `librecad/support/library/block/block4.dxf` | `FDA808DE85DB50622F3BDE2AD1B75527D0C366FAC7D06E0DABE614BB0A145286` | `LWPOLYLINE` из трёх вершин, `INSUNITS=0` |
| `librecad-v32-lwpolyline.dxf` | `librecad/support/library/plan/vegetation/v32.dxf` | `285755532D9A433D4FFA59206C907EC48902D7317B0BBCDB07BBD0BF4676732B` | пять `LWPOLYLINE` в миллиметрах (`INSUNITS=4`) |

Исходник: <https://github.com/LibreCAD/LibreCAD/tree/a05b4261f61d61e23d8c27c8d869ede756c2ac2b>

Контрольные суммы вычислены после нормализации окончаний строк к LF: это делает
проверку одинаковой при Windows `core.autocrlf=true` и на Linux CI.

LibreCAD распространяет эти материалы по GNU GPL v2. Файлы хранятся только как
тестовые данные, а их точное происхождение и контрольные суммы зафиксированы
здесь, чтобы обновление было явным и проверяемым.

# План безопасного исправления архитектурных недостатков GCodeGenerator

> **Как пользоваться:** отмечайте выполненные пункты галочкой (`- [x]`), дату и (по возможности) ссылку на коммит/PR.
> Формат отметки: `- [x] **Пункт** — 2026-08-22, commit abc1234 / PR #N`

## Статус по фазам

| Фаза | Название | Статус |
|------|----------|--------|
| 0 | Сеть безопасности (тесты, CI, golden) | ✅ готово (0.1–0.8: 48 тестов, 29 golden + эталонный набор) |
| 1 | Миграция платформы на .NET 10 (D5) | ✅ готово (1.1–1.6, 2026-08-22/23) |
| 2 | Структурное разделение (Core) | ✅ готово (2.1–2.3, 2026-08-23) |
| 3 | Очистка доменной модели | ☐ не начата |
| 4 | Структурный G-код и генераторы | ☐ не начата |
| 5 | Доработка карманов: стратегии + roughing/finishing (D1) | ☐ не начата |
| 6 | Предпросмотр без ре-парсинга | ☐ не начата |
| 7 | MVVM-гигиена | ☐ не начата |
| 8 | Персистентность, настройки, локализация | ☐ не начата |

---

## 0. Принципы безопасности (обязательны для каждого шага)

1. **Сначала сеть безопасности, потом рефакторинг.** Любое изменение поведения генератора/модели — только после фиксации текущего поведения тестами.
2. **Одна забота на коммит/PR.** Каждый шаг независимо собирается, проходит тесты и может быть откатан (`git revert`).
3. **Совместимость `.ygc` — жёсткое требование.** Старые файлы проектов обязаны открываться после каждого релиза.
4. **Совместимость G-кода — жёсткое требование.** Для фиксированных входов вывод генератора не меняется без явного решения (это код для станков). *Исключение: новые стратегии карманов (фаза 5) — новая функциональность, её вывод фиксируется собственными тестами, а не golden-сравнением со старым.*
5. **Изменения, видимые пользователю** (OK/Cancel в диалогах, единый список операций, новые опции карманов, смена ОС-требований) — отдельными шагами с полным ручным прогоном чек-листа (§8).

## 1. Решения, принятые заранее

| # | Решение | Варианты | Выбрано | Дата |
|---|---------|----------|---------|------|
| D1 | Неработающие опции карманов (стратегии Concentric/Radial/ZigZag/Lines, roughing/finishing) | (а) реализовать, (б) заблокировать в UI | ✅ **(а) Реализовать** (фаза 5) | 2026-08-22 |
| D2 | Финальный стек | WPF / Avalonia | ✅ **Остаёмся на WPF.** Переход на Avalonia — отдельный проект, планируется после полного завершения всех работ в WPF (в настоящий план не входит) | 2026-08-22 |
| D3 | Фреймворк MVVM | MugenMvvmToolkit до конца / CommunityToolkit.Mvvm | ✅ **CommunityToolkit.Mvvm** (фаза 1, п. 1.3) | 2026-08-22 |
| D4 | Настройки шпинделя/СОЖ в файле проекта | включать в `.ygc` / оставить глобальными | ✅ **Включаем в `.ygc`** (фаза 8, п. 8.2) | 2026-08-22 |
| D5 | Целевая платформа | net481 (Windows 7+) / .NET 10 WPF | ✅ **.NET 10 (net10.0-windows); поддержка Windows 7 не требуется.** Миграция — фаза 1, серией малых PR. Требования: Windows 10 22H2+ / Windows 11 (точную минимальную сборку зафиксировать в п. 1.6) | 2026-08-22 |

---

## Фаза 0. Сеть безопасности (3–5 дней) — без изменения кода продукта

**Цель:** зафиксировать текущее поведение, чтобы любой последующий шаг был проверяем.

- [x] **0.1** CI-пайплайн (GitHub Actions, `windows-latest`): сборка `GCodeGenerator.sln` (Release) + запуск тестов. Гейт на каждый push/PR. — 2026-08-22, commit 46bfa5a + 8319c9c (`.github/workflows/ci.yml`; фикс: `microsoft/setup-msbuild@v2`, т.к. msbuild не в PATH на образе; `dotnet vstest`). *Примечание: после фазы 1 пайплайн упрощается до `dotnet build`/`dotnet test` (п. 1.5).*
- [x] **0.2** Тестовый проект `GCodeGenerator.Tests` (TFM = TFM основного проекта, `ProjectReference`). — 2026-08-22, commit 6044047. *Отклонение от плана: вместо xUnit — **MSTest 3.1.1** (MSTest.TestFramework + MSTest.TestAdapter + Microsoft.NET.Test.Sdk 17.8.0): пакеты xunit в локальном NuGet-зеркале (localhost:8624) повреждены (stub'ы без lib/), сеть отдаёт зеркало вместо nuget.org. Если зеркало починят — можно вернуться к xUnit (механическая замена атрибутов). CI не зависит от этого (на раннере — nuget.org).*
- [x] **0.3** Фикстуры операций (`Tests/Fixtures`): 9 видов сверления, 6 профилей, 4 кармана (включая DXF-карман и DXF-профиль с образцовыми `.dxf` в `Tests/Assets`), варианты настроек (линейные номера вкл/выкл, padded G, AllowArcs вкл/выкл, шпиндель/СОЖ вкл/выкл, G54–G59, G92-старт/финиш). — 2026-08-22, commit a281502. *Примечания: (а) фикстуры сверления повторяют формулы `RebuildHoles` из VM и ключи `Metadata` из `OnClosed` (до типизации в фазе 3); (б) DXF-ассеты парсятся реальными парсерами продукта через `InternalsVisibleTo` + `internal` (`ParseDxfLines`/`ParseDxfClosedContours`) — поведение продукта не изменено; (в) `pocket_sample.dxf` — 2 замкнутых контура из LINE (CIRCLE не включён: парсер дублирует CIRCLE-контур — см. баг ниже); (г) найден баг: CIRCLE в DXF-кармане дублируется (`allPolylines` + `connectedContours` без дедупликации), LWPOLYLINE теряет вершины после первой — к исправлению в фазе 4 (декомпозиция `UnifiedPocketGenerator`).*
- [x] **0.4** Золотые тесты генератора: для каждой фикстуры `SimpleGCodeGenerator.Generate(...)` → golden-файлы `Tests/Golden/*.nc`, сравнение построчно (инвариантная культура). — 2026-08-22, commit ba3e5e4. *Примечания: (а) 29 golden-файлов по всем фикстурам 0.3; (б) генерация в тестах — `CultureInfo.InvariantCulture`: `GetDescription()` форматирует double через текущую культуру (комментарные строки) — баг продукта, фикс в поздней фазе (сменит вывод на машинах с запятой-разделителем, поэтому не чиним в фазе 0); (в) обновление эталонов — `GCG_WRITE_GOLDEN=1` + тест `Write_Golden_Files` (в CI no-op); (г) при добавлении фикстур в 0.5/фазе 5 golden-файлы создаются тем же механизмом; для новых стратегий карманов (фаза 5) — собственные тесты, не golden (принцип 4).*
- [x] **0.5** Характеризационные тесты рискованной логики. — 2026-08-22, commit 47b8d76 (`RiskyLogicTests.cs`, 22 теста; всего 38).
  - [x] DXF-карман: отсечка слоя («песочные часы», критерии площади/обхода/векторов), крайние случаи (узкий трапециевидный контур с уклоном, вырождение в точку);
  - [x] спираль при малых шагах и малом контуре;
  - [x] уклон стенок (taper) на профилях и карманах;
  - [x] дуги G2/G3 и fallback на полилинии при `AllowArcs=false`.
  *Примечания: (а) тесты вызывают генераторы напрямую (`UnifiedPocketGenerator`/`UnifiedProfileGenerator` с `List<string>`-коллектором) — уровень, на котором живёт тестируемая логика; (б) у **профилей нет параметра taper** (`IProfileOperation`/модели профилей не имеют `WallTaperAngleDeg` — только карманы), поэтому taper покрыт только для карманов; (в) формула слоя «песочных часов» (`ceil(log(0.01)/log(ratio))+1`) для выпуклых контуров с линейным уклоном никогда не срабатывает первой (первыми побеждают критерий 1 — рост площади от bowtie — или `IsContourTooSmall`), эффект характеризуется косвенно через наблюдаемые слои остановки; (г) **новые зафиксированные баги** (исправить в фазе 4/5): 1) «фантомная фрезеровка» — контур меньше фрезы фрезеруется (оффсет 2×2 квадрата на 1.5 — bowtie с положительной shoelace-площадью ≈ 1.0, проходит все критерии отсечки; T9/T4), 2) слепое пятно критериев: оффсет квадрата за вырождением деградирует в маленький однонаправленный квадрат — площадь/обход/векторы его не видят (даже при o=5.1 > вписанного радиуса), 3) no-op guard `step < 1e-6` в `GCodeGenerationHelper.CalculateStep` (переприсваивает то же значение).
- [x] **0.6** Тест round-trip проекта: вынести сериализацию `.ygc` из `MainViewModel` в тестируемый класс `ProjectFileService`; сохранить фикстуру → открыть → сравнить операции по полям. — 2026-08-22, commit c5abb36 (`Services/ProjectFileService.cs` + `ProjectFileServiceTests.cs`, 7 тестов; всего 45).
  *Примечания: (а) формат файла не изменён (JavaScriptSerializer, UTF-8, `{"Operations":[{Type,Data}]}`) — старые `.ygc` читаются, миграция на System.Text.Json — п. 1.2; (б) DTO `ProjectData`/`SerializableOperation` из private-вложенных в MainViewModel стали публичными top-level (часть контракта формата); (в) round-trip проверен для всех 19 операций фикстур 0.3 (15 типов) со сравнением всех публичных свойств рекурсивно; (г) зафиксировано поведение: валидный тип + не-объектный JSON данных бросает исключение (в UI — «Ошибка при загрузке проекта»), а не пропускается молча; int-значения Metadata после загрузки приходят double (учтено в сравнении).
- [x] **0.7** Снять «ручной» эталон: сгенерировать G-код для реальных проектов (если есть) и сохранить как эталонный набор. — 2026-08-22, commit 38714e7 (`Reference/reference_project.ygc` + `reference_project.nc` + `ReferenceProjectTests.cs`, 3 теста; всего 48).
  *Примечания: (а) реальных `.ygc`-проектов на машине не найдено (проверены репозиторий, диск F:, Documents/Desktop/Downloads/source/AppData) — эталон снят с представительного многооперационного проекта: все 19 операций фикстур 0.3 (15 типов) в одном `.ygc`, настройки — значения по умолчанию `GCodeSettings`; (б) тест идёт через полный реальный пайплайн: файл `.ygc` → `ProjectFileService` → `SimpleGCodeGenerator` → построчное сравнение с `.nc` (в отличие от golden 0.4 — in-memory фикстуры); (в) `DxfFilePath` в эталонном `.ygc` нормализован к именам ассетов (без машинного пути); перегенерация — `GCG_WRITE_REFERENCE=1` + `Write_Reference_Set`; (г) зафиксирована латентная уязвимость формата: `.ygc` хранит `AssemblyQualifiedName` с версией сборки (локальные/CI-сборки — 0.0.0.0), файл, сохранённый сборкой с версией (YBUILD_PRODUCT_VERSION_DOTNET), не откроется в сборке 0.0.0.0 (типы не разрешаются, операции пропускаются молча) — устраняется в п. 1.2 (короткие имена типов).
- [x] **0.8** Документ `docs/SMOKE_CHECKLIST.md` (чек-лист §8) прогнан вручную один раз «as-is». — 2026-08-22, commit 4d97197 (`docs/SMOKE_CHECKLIST.md`).
  *Примечания: (а) `docs/SMOKE_CHECKLIST.md`: чек-лист §8 + подготовка, ожидаемые результаты по пунктам, журнал прогона, список известных проблем «as-is»; пункты с автоматизированными аналогами помечены (эталон 0.7, round-trip 0.6, golden 0.4); (б) запуск приложения проверен (Release-сборка, старт без ошибок, 2026-08-22); интерактивный «as-is» прогон — пользователем по документу (результат — в журнале документа).

**DoD фазы 0:** ✅ CI настроен (локально: сборка + 48 тестов зелёные; прогон на GitHub — после пуша); ✅ ≥ 40 тестов (48); ✅ golden-файлы зафиксированы в репозитории (29 golden + эталонный набор 0.7); ✅ smoke-чек-лист: документ `docs/SMOKE_CHECKLIST.md` готов, запуск проверен, интерактивный «as-is» прогон — пользователем (журнал в документе).

---

## Фаза 1. Миграция платформы на .NET 10 (D5) (8–12 дней)

**Цель:** современный стек (net10.0-windows) серией независимых зелёных PR. Каждый подшаг — отдельный коммит/PR с проверкой (сборка + тесты + smoke).
**Почему сейчас:** (а) CI упрощается до `dotnet build`/`dotnet test`; (б) две net48-only зависимости всё равно придётся убирать — `JavaScriptSerializer` (System.Web.Extensions отсутствует в .NET) и MugenMvvmToolkit (net45, абандон); (в) System.Text.Json, CommunityToolkit.Mvvm, MahApps 2.x, C# 14 — first-class.

- [x] **1.1** SDK-style csproj + PackageReference (TFM пока без изменений — net481). Проверить: resx, `Settings.settings`, иконка, binding redirects. — 2026-08-22, commit 7a2a1b6.
  *Примечания: (а) `AppendTargetFrameworkToOutputPath=false` — классическая раскладка `bin\Release` без подпапки TFM (CI-пути и документация без изменений); (б) `LangVersion=latest` — как до миграции (в старом csproj LangVersion не задан); (в) `GenerateAssemblyInfo=false` — `Properties/AssemblyInfo.cs` сохранён (в т.ч. InternalsVisibleTo); (г) проверено: resx встроены (Properties.Resources + LocalizableResources), Settings.settings работает (приложение читает настройки), иконка встроена в exe, `GCodeGenerator.exe.config` идентичен старому App.config (redirect Autofac), у тестов — автогенерация redirects для MSTest; (д) обходная явная ссылка на MSTest.TestAdapter из старого тестового csproj убрана (PackageReference сам размещает адаптер); (е) SDK-style автоматически включил 4 устаревших файла после переноса Views/Drill (b9ff348) — исключены `<Compile/Page Remove>` (Views/DrillOperationsView.xaml объявляет тот же класс, что и Views/Drill/DrillOperationsView.xaml), кандидаты на удаление в фазе 2/7; (ж) CI без изменений: `nuget restore` поддерживает SDK-проекты, `msbuild`/`dotnet vstest` работают (упрощение CI — п. 1.5); (з) 48/48 тестов, golden без изменений (вывод G-кода идентичен).
- [x] **1.2** Заменить `JavaScriptSerializer` (System.Web.Extensions) на **System.Text.Json** в `ProjectFileService` (из 0.6): схема с полем `version`, явный дискриминатор типов (короткие имена из белого списка, не `AssemblyQualifiedName`). **Легаси-ридер:** старые файлы (JSON от JavaScriptSerializer — обычный JSON) читаются и мигрируются при открытии; сохранение — всегда в новом формате. Тесты round-trip + эталонные старые файлы. — 2026-08-22, commit 5bfa73c (53 теста; golden и эталонный `.nc` без изменений).
  *Примечания: (а) формат v2: `{"version":2,"operations":[{"type":"<короткое имя>","data":{...}}]}` — конверт camelCase, payload PascalCase как в модели; короткий дискриминатор из белого списка `OperationTypeNames` (11 типов) вместо `AssemblyQualifiedName`; (б) легаси-ридер: v1 определяется отсутствием поля `version`, тип разрешается по имени класса из AQN (часть до запятой → после последней точки), **версия сборки игнорируется** — устраняет уязвимость версий из 0.7 (файл сборки с версией теперь открывается); (в) `PrimitiveDictionaryConverter` (поле `Metadata`): **точно повторяет JavaScriptSerializer** — целое→`Int32`/`Int64`, дробное→`Decimal`, строка→`string`, bool→`bool`, null→`null`, enum→`Int32`; критично, что enum читается как `Int32`, т.к. VM приводят их прямым кастом `(MillingDirection)Metadata["Direction"]` (double упал бы с `InvalidCastException`); (г) `DoubleJsonConverter`: краткий round-trip double (формат `R`) — на .NET Framework STJ по умолчанию пишет `0.3` как `0.29999999999999999`, исправлено; форматирование чисел в v2 совпадает с v1 (76 значений проверено); (д) убраны ссылки `System.Web`/`System.Web.Extensions`, добавлен `System.Text.Json 9.0.19` (net462-совместим; в 1.5 при переходе на net10.0 можно поднять версию); (е) тесты: round-trip v2 (19 операций, все поля), структура v2, пропуск некорректных записей, не-объектный data→исключение, нет секции→null, пустой массив→пустой список, некорректный JSON→исключение, легаси: загрузка `legacy_project_v1.ygc` (19 операций), поля совпадают с in-memory эталоном, AQN с Version=9.9.9.9 открывается, миграция v1→v2 при сохранении; (ж) `Reference/reference_project.ygc` перегенерирован в v2 (через `GCG_WRITE_REFERENCE=1`), `reference_project.nc` **байт-в-байт идентичен**; новый эталонный легаси-файл `Reference/legacy_project_v1.ygc` (копия v1 до 1.2).
- [x] **1.3** MugenMvvmToolkit → **CommunityToolkit.Mvvm** (D3) — 2026-08-22, commit 0ce9e96 (часть 1: локализация) + 374bb55 (часть 2: MVVM-фреймворк).
  - [x] базовые классы: `ViewModelBase` → `ObservableObject`; `CloseableViewModel` → собственный базовый класс диалога. — 2026-08-22, commit 374bb55. *Примечание: `ViewModelBase : ObservableObject` (CommunityToolkit.Mvvm.ComponentModel) — безаргументный `OnPropertyChanged()` (с `[CallerMemberName]`) совместим с Mugen, объявления свойств в 25 VM не менялись; `CloseableViewModel.OnClosed()` — public, без параметра `IDataContext` (не использовался ни в одном из 21 диалогового VM); добавлен `IHasDisplayName` (замена `MugenMvvmToolkit.Interfaces.Models.IHasDisplayName`).*
  - [x] свой `RelayCommand` → `RelayCommand`/`AsyncRelayCommand`. — 2026-08-22, commit 374bb55. *Примечание: удалён `Infrastructure/RelayCommand.cs`; `RaiseCanExecuteChanged()` → `NotifyCanExecuteChanged()` (метод `IRelayCommand`); **важно: в CommunityToolkit.Mvvm 8.x классы команд находятся в namespace `CommunityToolkit.Mvvm.Input`** (`RelayCommand`, `AsyncRelayCommand`, `IRelayCommand`), а не в `CommunityToolkit.Mvvm` — отсюда `using CommunityToolkit.Mvvm.Input;` в 7 VM с командами (`ObservableObject` — в `CommunityToolkit.Mvvm.ComponentModel`); `AsyncRelayCommand` не потребовался (в VM нет async-команд).*
  - [x] composition root: `Bootstrapper`/`BootstrapperEx`/`GCodeGeneratorMvvmApp` → прямой Autofac в `App.xaml.cs`. — 2026-08-22, commit 374bb55. *Примечание: удалены `GCodeGeneratorMvvmApp.cs` и `LocalizationModule.cs`; `App.xaml.cs`: `AppLocalizationManager` + `PlatformVariables` (как в `LocalizationModule.Load`), Autofac: `ILocalizationManager` (instance), `WpfDialogService` (SingleInstance), `RegisterAssemblyTypes(...).AssignableTo<ViewModelBase>().InstancePerDependency()`; главное окно — `scope.Resolve<MainViewModel>()` + `new MainView { DataContext = ... }`.*
  - [x] `IDialogService` (WPF-реализация: `ShowInfo/ShowError/ShowConfirm`, `ShowSaveDialog/ShowOpenDialog`) — замена `GetViewModel<T>()` + `ShowAsync()` и `MessageBox`/`FileDialog` в VM. — 2026-08-22, commit 374bb55. *Примечание: `WpfDialogService` (Autofac `ILifetimeScope`): `CreateViewModel<T>()` = `scope.Resolve<T>()`, `ShowDialog<T>()` = view по конвенции `ViewModels[.Sub].XxxViewModel → Views[.Sub].XxxView` (все диалоговые view — `Window`), синхронный `ShowDialog()`, затем `OnClosed()`; внедрён в 6 VM (Main, Drill/ProfileMilling/Pocket Operations + ProfileDxf/PocketDxf Operation); 3 operations-VM создаются `new` в конструкторе MainViewModel — `IDialogService` передаётся явно; проверено вживую: диалоги операций и настроек открываются/закрываются, операция добавляется в список, исключений нет.*
  - [x] XAML: связки `{DataBinding '$i18n.Key'}` (Mugen Binding) → собственный механизм локализации (MarkupExtension/конвертер + `INotifyPropertyChanged`-провайдер), механическая замена по всем XAML (~40 файлов). — 2026-08-22, commit 0ce9e96. *Примечание: `LocExtension` возвращает локализованную строку (а не WPF `Binding`+конвертер) — нативная привязка со статическим `Source` некорректно применяется при загрузке вложенных UserControls в приложении на базе Mugen (зависание при старте, окно не отображается); культура не меняется во время выполнения (нет вызовов `ChangeCulture`), поэтому динамическое обновление не требуется. `AppLocalizationManager` реализует `INotifyPropertyChanged` (задел на будущее). Убран `MugenLocalizationManager` (в т.ч. неиспользуемый `TimeToKindString`). Проверено: сборка, 53/53 теста, старт приложения, рендер главного окна и кнопок.*
- [x] **1.4** MahApps.Metro 2.x (+ обновление ControlzEx, Microsoft.Xaml.Behaviors). — 2026-08-22, commit 0f4cf8a.
  *Примечания: (а) версии (выбор исполнителя — последние стабильные 2.x + совместимые зависимости): MahApps.Metro **2.4.11**, ControlzEx **4.4.0** (входит в диапазон зависимости MahApps 2.4.11 для net47: `[4.4.0, 6.0.0)`), Microsoft.Xaml.Behaviors.Wpf **1.1.142**; (б) новый `NuGet.config` в корне репозитория: локальное ProGet-зеркало + **nuget.org** — MahApps 2.x отсутствует в зеркале (404 на все 2.x), nuget.org — фолбэк; `<clear/>` намеренно нет (источники пользователя сохраняются); CI (`nuget restore`) подхватывает репозиторный конфиг автоматически; (в) App.xaml: MergedDictionaries — официальный набор 2.4.11 (`Styles/Controls.xaml` + `Styles/Fonts.xaml` + `Styles/Themes/Light.Blue.xaml` — combined base+accent в одном файле; тёмная — `Dark.Blue.xaml`), заменил 14 pack-URI 1.x; (г) implicit-стиль: в 2.x стиль MetroTabItem типизирован под `mah:MetroTabItem` (в 1.x — строковый ключ `MetroTabItem` с `TargetType=TabItem`, поэтому BasedOn из старого App.xaml невозможен) — в XAML `TabItem` → `mah:MetroTabItem` (3 в MainView, 4 в SettingsView); выбранная вкладка сохраняет вид 1.x (акцентный фон + жирный текст `MahApps.Brushes.IdealForeground`), невыбранная — дефолт 2.x (тема-зависимый серый, читаем в тёмной теме); (д) `ThemeHelper`: `ThemeManager` перенесён из MahApps.Metro в **ControlzEx** (`ControlzEx.Theming.ThemeManager.Current`); `GetAccent/GetAppTheme/ChangeAppStyle/DetectAppStyle` в 2.x отсутствуют — заменены на `GetTheme(base, color)` + `ChangeTheme(app, base, color)`; темы MahApps регистрируются через `MahAppsLibraryThemeProvider.DefaultInstance` (идемпотентная регистрация); (е) `ToggleSwitch` 2.x: `OnLabel/OffLabel/IsChecked` → `OnContent/OffContent/IsOn` (two-way by default); (ж) ремэп ключей: `MetroTextBox` → `MahApps.Styles.TextBox` (11 мест), `MetroComboBox` → `MahApps.Styles.ComboBox` (2), `ControlBackgroundBrush/TextBrush/TextBoxBorderBrush` → `MahApps.Brushes.Control.Background/Text/TextBox.Border`; `TextBrush` (~100 использований в OperationImages.xaml + `TryFindResource` в code-behind) — локальный псевдоним в App.xaml, привязанный к теме (`Color="{DynamicResource MahApps.Colors.ThemeForeground}"`); (з) проверено: Release-сборка (только 2 предсуществующих warning), 53/53 теста, запуск, UIA-смоук: главное окно + 3 вкладки, диалог настроек (4 вкладки, выбор вкладки), диалог операции открывается/закрывается, **смена Light/Dark темы во время выполнения работает end-to-end** (фон окна 255,255,255 → 37,37,37, ToggleState Off→On, тема применяется ко всему приложению; после теста состояние возвращено в светлую). Вывод G-кода и формат `.ygc` не тронуты.*
- [x] **1.5** Переключение TFM на **net10.0-windows**; `Properties.Settings` — через пакет System.Configuration.ConfigurationManager. CI: `actions/setup-dotnet` + `dotnet build` / `dotnet test` (убрать шаги msbuild/nuget/targeting-pack). — 2026-08-23, commit 63e7cb7.
  *Примечания: (а) TFM **net10.0-windows** в обоих csproj (основной + тесты, D5); `AppendTargetFrameworkToOutputPath=false` сохранён — классическая раскладка `bin\Release` без подпапки TFM (CI/документация без изменений); exe — apphost (.NET 10): `GCodeGenerator.exe` + `GCodeGenerator.dll` + `runtimeconfig.json`; (б) **отклонение от формулировки плана**: пакеты `System.Text.Json 9.0.19` и `System.Configuration.ConfigurationManager` **убраны** — на net10 оба входят в shared framework (warning NU1510 подтверждает; `System.Configuration.ConfigurationManager.dll` — в Microsoft.WindowsDesktop.App.Ref 10.0.10, `System.Text.Json.dll` — в Microsoft.NETCore.App.Ref 10.0.10), отдельные пакеты не нужны; STJ теперь **10.x из рантайма** (формат `.ygc` проверен тестами 1.2 и неизменным эталонным файлом); (в) `Properties.Settings` (ApplicationSettingsBase, 27 пользовательских настроек) работает из коробки; **последствие D5: место файла настроек меняется** (новый хэш-каталог под `%LOCALAPPDATA%\GCodeGenerator\`) — одноразовый сброс пользовательских настроек при первом запуске новой версии; (г) `App.config` удалён (`git rm`): содержал только `<startup><supportedRuntime>` и binding redirect Autofac — оба не применяются на .NET 10; `ConfigurationManager`/`AppSettings` в коде не используются (grep); (д) убраны 6 net48 framework-ссылок (`System.ComponentModel.DataAnnotations`, `System.Configuration`, `System.Data.DataSetExtensions`, `System.Net.Http`, `System.Windows`, `System.Xaml`) — не используются в коде (grep), все входят в shared framework / WPF SDK; `AutoGenerateBindingRedirects` убран из обоих csproj (net48-only); (е) тестовый csproj: `Microsoft.NET.Test.Sdk` 17.8.0 → **18.9.0** (поддержка net10.0), **MugenMvvmToolkit 6.5.0 убран** (остаток до-1.3, тесты его не используют — DoD фазы 1 «нет ссылок на Mugen» выполнен полностью); (ж) **фикс -0.000**: math-библиотека .NET 10 в местах, где .NET Framework давал 0.0, возвращает -0.0 или крошечный остаток (напр. `10*Math.Cos(3π/2)` = -1.837e-15 → форматировалось `X-0.000` вместо `X0.000` — 2 golden-теста упали после переключения TFM); централизовано в `GCodeGenerationHelper.FormatNumber(double, string)`: `Math.Round` до числа знаков из fmt + нормализация -0.0 → 0.0 (для всех ненулевых значений результат идентичен прежнему инлайн-форматированию); 56 инлайн-вызовов `.ToString(fmt, culture)` в 5 генераторах заменены на `FormatNumber`; убраны 12 локальных `var culture` и параметр `CultureInfo culture` из 7 сигнатур (`GenerateDxfLayerWithSpiral`, `GenerateSpiralStrategy`, `GenerateRampEntry`, `FollowContourFull`, `FollowContourFromPoint`, `GenerateEquidistantContour` + call-sites) и `using System.Globalization` из 5 файлов; `SimpleGCodeGenerator` не тронут (форматирует пользовательские значения настроек напрямую, тригонометрических остатков там нет); полная централизация форматтера — задача фазы 4; (з) CI переписан: `windows-latest` + `actions/setup-dotnet@v4` (dotnet-version 10.0.x) + `dotnet restore` / `dotnet build -c Release` / `dotnet test --no-build`; шаги msbuild/nuget/targeting-pack удалены; (и) проверено: Release-сборка (только 2 предсуществующих warning: CS1717 PreviewView.xaml.cs:75, CS0219 PocketDxfOperationViewModel.cs:1441), **53/53 теста на net10.0**, golden-файлы без изменений, `Reference/reference_project.ygc` **неизменён** после прогона тестов (вывод STJ 10.x идентичен 9.x), UIA-смоук: главное окно + 3 вкладки, диалог настроек (4 вкладки, выбор вкладки), диалог операции открывается/закрывается, смена Light/Dark темы (255,255,255 → 37,37,37 → 255,255,255), приложение закрывается чисто.*
- [x] **1.6** README: требования ОС (Windows 10 22H2+ / Windows 11 — зафиксировать точную минимальную сборку), .NET 10 Desktop Runtime (или self-contained-установщик), обновление разделов «Требования» и «Сборка из исходников». — 2026-08-23, commit 586ccce.
  *Примечания: (а) раздел «Требования»: **Windows 11 24H2 (build 26100) или новее** — рекомендуемая платформа и минимальная поддерживаемая Windows 11 (23H2 — только Enterprise, поддержка до 10.11.2026); **Windows 10 22H2 (build 19045) или новее** — минимальная Windows 10 по D5; **.NET 10 Desktop Runtime** (10.0.x) — для запуска (ссылка на dotnet.microsoft.com; self-contained-установщик — уточняется к релизу); **.NET 10 SDK** — для сборки; (б) **важное уточнение (отклонение от допущения D5)**: по официальному списку поддерживаемых ОС .NET 10 ([release-notes/10.0/supported-os.md](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md), обновлён 2026-04-06) **Windows 10 22H2 не входит в поддерживаемые ОС** — Windows 10 достиг EOL 14.10.2025, 22H2 перенесена в «Out of support»; официально поддерживаемые Windows 10: 21H2 Enterprise LTSC (build 19044, до 12.01.2027), 21H2 IoT LTSC, 1809/1607 LTSC. README сохраняет 22H2 как минимальное требование продукта (утверждённый план) с честным примечанием об официальной политике и рекомендацией Windows 11 24H2; (в) раздел «Сборка из исходников»: `.NET 10 SDK` + `dotnet build GCodeGenerator.sln -c Release` + `dotnet test GCodeGenerator.Tests/GCodeGenerator.Tests.csproj -c Release --no-build` (команды проверены: сборка + 53/53), путь к приложению `GCodeGenerator\bin\Release\GCodeGenerator.exe`, Visual Studio 2026 как альтернатива; (г) заодно обновлены устаревшие разделы: «Технологии» (.NET 10 / CommunityToolkit.Mvvm / MahApps.Metro вместо .NET Framework 4.8.1 / MugenMvvmToolkit), «Структура проекта» (устаревший `packages/` → `GCodeGenerator.Tests/`, `docs/`, `Plan.md`); (д) исправлены плейсхолдеры `yourusername` в ссылках (Releases/clone/Issue) на фактический репозиторий `AlexanderYunker1983/GCodeGenerator` (из `git remote`).*

**DoD фазы 1:** ✅ все тесты зелёные (53/53 на net10.0, включая round-trip старых `.ygc` — 1.2); ✅ golden без изменений (проверено в 1.5); ✅ в проекте нет ссылок на MugenMvvmToolkit и System.Web.Extensions (1.3/1.5; grep: только исторические упоминания в комментариях); ✅ CI зелёный на `dotnet build`/`dotnet test` (локально — 1.5/1.6; прогон на GitHub — после пуша); ✅ smoke-чек-лист §8 зелёный (UIA-смоук 1.4/1.5: окно, 3 вкладки, диалоги настроек/операции, смена темы, чистый выход); ✅ README актуален (1.6).

**Риски:** Mugen на .NET — исключён заменой в 1.3 до переключения TFM; JS-сериализатор — заменён в 1.2; `Properties.Settings` — поддерживается (на net10 — из shared framework, см. 1.5(в): одноразовый сдвиг файла настроек). Откат каждого подшага — revert одного PR.

---

## Фаза 2. Структурное разделение: выделение Core (3–5 дней)

**Цель:** ядро продукта (модели + генерация) вынести из WPF-сборки.

- [x] **2.1** Новый проект `GCodeGenerator.Core` (класс-библиотека, TFM = TFM приложения, без WPF): — 2026-08-23, commit 7dc78d9.
  - [x] `Models/*` (кроме `GCodeSettingsStore` — остаётся в App как инфраструктура пользовательских настроек); — 2026-08-23, commit 7dc78d9. *Примечание: перенесено 24 модели (все, кроме `GCodeSettingsStore`, который использует `Properties.Settings`).*
  - [x] `GCodeGenerators/*` (генераторы, геометрия, хелперы, интерфейсы); — 2026-08-23, commit 7dc78d9. *Примечание: 25 файлов — 6 генераторов/интерфейсов верхнего уровня + 14 `Geometry/` + 3 `Helpers/` + 2 `Interfaces/`.*
  - [x] `ILocalizationManager` + базовый `LocalizationManager` (без зависимостей UI-стека). — 2026-08-23, commit 7dc78d9. *Примечание: `LocalizationManager` — чистый BCL (System.Reflection/Resources, ресурсы загружаются по имени сборки в рантайме); WPF-часть механизма локализации (`AppLocalizationManager`, `LocalizationProvider`, `LocExtension`) остаётся в App.*
  *Примечания (проект): (а) SDK-класс-библиотека, TFM **net10.0-windows** (как у приложения, по плану), без UseWPF, без пакетов NuGet — только BCL (System.Collections.Generic, System.ComponentModel, System.Globalization, System.Linq, System.Reflection, System.Resources); (б) **неймспейсы сохранены** (`GCodeGenerator.Models`, `GCodeGenerator.GCodeGenerators.*`, `GCodeGenerator.Localization`) — код App/тестов не менялся; переименование неймспеев — отдельный шаг (в пункт не входило); (в) `GenerateTargetPlatformAttribute/GenerateSupportedOSPlatformAttribute=false`: SDK для net10.0-windows по умолчанию генерирует `[assembly: SupportedOSPlatform("Windows7.0")]`, а в App/Tests (ручной AssemblyInfo.cs, GenerateAssemblyInfo=false) таких атрибутов нет — рассогласование дало 6056 предупреждений CA1416 в App/Tests; код Core — чистый кроссплатформенный BCL, аннотация «только Windows» убрана; (г) перенос — `git mv` (100% rename, история файлов сохранена); (д) в перенесённых файлах нет internal-членов → InternalsVisibleTo в Core не требуется (internal-парсеры DXF — в VM App).*
- [x] **2.2** App ссылается на Core (`ProjectReference`); перенесённые файлы удалены из App. — 2026-08-23, commit 7dc78d9.
  *Примечания: (а) `GCodeGenerator.csproj`: `ProjectReference` на Core; решение: проект Core добавлен (GUID F064B360-9F95-4DE7-B37C-285D074BFA0C); (б) **9 XAML-файлов**: `xmlns:models="clr-namespace:GCodeGenerator.Models"` → `clr-namespace:GCodeGenerator.Models;assembly=GCodeGenerator.Core` — XAML разрешает clr-namespace без `assembly=` в текущей сборке, после переноса типов без этого ошибки MC3050 (не удается найти тип); (в) тестовый проект не менялся: типы Core доступны транзитивно через ProjectReference на App, `InternalsVisibleTo("GCodeGenerator.Tests")` остался в App (нужен для internal-парсеров DXF в VM); (г) проверено: Release-сборка (только 2 предсуществующих warning), 53/53 теста, golden без изменений, UIA-смоук: окно + 3 вкладки, диалог настроек (4 вкладки), диалог операции, смена темы (255,255,255 → 37,37,37 → 255,255,255), чистый выход.*
- [x] **2.3** Проверка чистоты Core: нет ссылок на `PresentationCore`/`WindowsBase`/WPF (проверка в CI или ревью). — 2026-08-23, commit 265a3e8.
  *Примечания: (а) CI-шаг «Check Core purity (no WPF)» (pwsh): XML-парсинг `GCodeGenerator.Core.csproj` (нет `UseWPF`/`UseWindowsForms`) + grep исходников Core на WPF-using (`System.Windows*`, `System.Xaml`, `PresentationCore`, `WindowsBase`, `PresentationFramework`); проверен локально в обе стороны: чистый Core → pass, проба-файл с `using System.Windows;` → fail (exit 1); (б) локальная верификация: в `GCodeGenerator.Core.dll` нет строк PresentationCore/PresentationFramework/WindowsBase/System.Xaml/WinForms, в deps.json нет `Microsoft.WindowsDesktop.App`; (в) жёсткий гейт — сама сборка: без UseWPF WPF-типы компилятору недоступны, любая WPF-зависимость в исходниках Core не соберётся.*

**DoD фазы 2:** ✅ сборка + все тесты зелёные (53/53, 2.2); ✅ golden без изменений (2.2); ✅ Core без WPF-зависимостей (2.3: CI-чек + бинарная верификация); ✅ приложение работает (UIA-смоук, 2.2).

---

## Фаза 3. Очистка доменной модели (5–8 дней)

**Цель:** убрать `Metadata`-словари, двойной источник истины и name-based dispatch.

- [ ] **3.1** Сверление: в `DrillPointsOperation` добавить `DrillMode { Points, Line, Array, Rect, Circle, Arc, Polygon, Ellipse, Package }` + типизированные параметры (`StartX/Y/Z`, `Distance`, `HoleCount`, `AngleDeg`, `RowCount/ColCount`, `Radius`, `Sides` и т.д. — по фактическим ключам `Metadata`).
- [ ] **3.2** Миграция при загрузке `.ygc`: `Metadata` с ключами → типизированные свойства, `Metadata` очищается. Старые файлы открываются, новые сохраняются без `Metadata`.
- [ ] **3.3** VM сверления читают/пишут только типизированные свойства (без двойной записи).
- [ ] **3.4** `DrillOperationsViewModel.EditSelectedOperation`: `switch (drillOp.DrillMode)` вместо сравнения по `Name`. Тест: переименованная операция открывает верный диалог.
- [ ] **3.5** Профили: удалить двойную запись в `OnClosed` (только типизированные свойства); чтение — только из свойств; миграция из `Metadata` при загрузке.
- [ ] **3.6** Удалить `Metadata` из `Profile*Operation` (после зелёных golden и round-trip; на один релиз оставить `[Obsolete]`/`[JsonIgnore]`).
- [ ] **3.7** Валидация в домене: `IValidatable { IReadOnlyList<ValidationIssue> Validate(); }` в Core для всех операций (глубины, диаметры, шаги, количество отверстий, замкнутость контуров).
- [ ] **3.8** Защитные проверки в генераторах (`StepDepth <= 0` → `ArgumentException` вместо бесконечного цикла).
- [ ] **3.9** (Опционально) `OperationBase`: `INotifyPropertyChanged` вынести за интерфейс `IEnabledOperation` либо зафиксировать статус-кво с комментарием.

**DoD фазы 3:** ☐ все тесты зелёные; ☐ golden без изменений; ☐ тест миграции (старый `.ygc` → открыт → сохранён → `Metadata` пуст, значения сохранены); ☐ smoke-чек-лист §8 зелёный.

---

## Фаза 4. Структурный G-код и генераторы (8–12 дней) — ключевая фаза

**Цель:** убрать string round-trip, хрупкую рефлексию и god-class.

- [ ] **4.1** Структурированная модель программы в Core: `GCodeProgram { List<GCodeBlock> }`, `GCodeBlock { LineNumber, Words, Comment }`, `GCodeWord { Letter, Number, Text }` + `ProgramBuilder` (`RapidTo`, `LinearTo`, `ArcCW/CCW`, `Dwell`, `SpindleOn/Off`, `CoolantOn/Off`, `Comment`, `SetWcs`, `SetStart/EndPosition`).
- [ ] **4.2** `GCodeFormatter` в Core: `GCodeProgram → List<string>` с учётом `UseLineNumbers/LineNumberStart/Step`, `UsePaddedGCodes`, `UseComments` (перенос локальных функций `FormatG/FormatM/AddLine` из `SimpleGCodeGenerator`).
- [ ] **4.3** Дифференциальный тест: для всех фикстур старое (строки) == новое (структура → форматтер) построчно. Переключение только при 100% равенстве.
- [ ] **4.4** Порт генераторов на `ProgramBuilder` по одному за коммит (Drill → Profile → Pocket), после каждого — golden-тесты.
- [ ] **4.5** Явная регистрация: `IOperationGeneratorRegistry` (явные маппинги `Type → IOperationGenerator`, резолв через IoC); name-based рефлексия в `SimpleGCodeGenerator.LoadGenerators` удалена.
- [ ] **4.6** Декомпозиция `UnifiedPocketGenerator`:
  - [ ] `DxfPocketLayerGenerator` — слой DXF-кармана;
  - [ ] `ContourCutoffAnalyzer` — эвристики отсечки (площади, «песочные часы», обход, векторы) как чистый класс, ≥ 15 юнит-тестов;
  - [ ] `IPocketPocketingStrategy` + `SpiralPocketingStrategy` (инфраструктура для новых стратегий — реализация в фазе 5).
- [ ] **4.7** Опции стратегий ≠ Spiral и roughing/finishing в UI **временно заблокировать** (пометка «в разработке»); разблокировка — после фазы 5 (D1). Мёртвый `PocketGenerationHelper.ProcessRoughingFinishing` — доработать и подключить в фазе 5.

**DoD фазы 4:** ☐ `IOperationGenerator` работает через `ProgramBuilder`; ☐ форматтер покрывает 100% golden; ☐ рефлексивная регистрация удалена; ☐ `UnifiedPocketGenerator` < 400 строк; ☐ golden без изменений.

---

## Фаза 5. Доработка карманов: стратегии + roughing/finishing (D1) (8–12 дней)

**Цель:** реализовать объявленные в UI, но неработающие опции: стратегии Concentric, Radial, ZigZag, Lines и черновую/чистовую обработку.
**Важно:** это новая функциональность — её вывод не сравнивается со старым golden, а покрывается собственными поведенческими тестами (геометрия траектории).

- [ ] **5.1** Завершить `IPocketPocketingStrategy` (фаза 4): контракт `GenerateLayer(IPocketGeometry, слой, параметры) → блоки ProgramBuilder`; учёт taper, радиуса инструмента, шага, направления фрезерования (climb/conventional), `ContourCutoffAnalyzer`.
- [ ] **5.2** `ConcentricPocketingStrategy` — концентрические проходы вдоль эквидистантного контура.
- [ ] **5.3** `RadialPocketingStrategy` — радиальные проходы от центра к контуру (с учётом формы контура и DXF-контуров).
- [ ] **5.4** `ZigZagPocketingStrategy` — зигзаг (чёрпковые проходы с разворотом).
- [ ] **5.5** `LinesPocketingStrategy` — линии под углом `LineAngleDeg` (сечение контура прямыми, обработка островов/разрывов).
- [ ] **5.6** Roughing/finishing: доработать и подключить `ProcessRoughingFinishing` (черновая с припуском `FinishAllowance`, чистовая: Walls / Bottom / All, `PocketFinishingMode`); корректная работа в связке с каждой стратегией и DXF-карманами.
- [ ] **5.7** Тесты:
  - [ ] юнит-тесты каждой стратегии (покрытие области, шаг, отсутствие выхода за контур, корректные Z-переходы между проходами);
  - [ ] интеграционные тесты «операция → G-код» для каждой стратегии × (прямоугольник, круг, эллипс, DXF);
  - [ ] тесты roughing/finishing (припуск по контуру и глубине, режимы Walls/Bottom/All, карман «слишком маленький после припуска»).
- [ ] **5.8** Разблокировать опции в UI (снять пометку «в разработке» из 4.7); ручной прогон: каждая стратегия на каждом типе кармана + roughing/finishing.
- [ ] **5.9** Документация: описание стратегий и roughing/finishing в README.

**DoD фазы 5:** ☐ 5 стратегий + roughing/finishing работают на всех типах карманов (включая DXF); ☐ тесты по 5.7 зелёные; ☐ существующий golden (Spiral) без изменений; ☐ smoke-чек-лист §8 зелёный; ☐ README обновлён.

---

## Фаза 6. Предпросмотр без ре-парсинга (5–7 дней)

**Цель:** 2D/3D превью потребляют структуру, а не текст; WPF-объекты вне VM.

- [ ] **6.1** `PreviewViewModel` получает `GCodeProgram` (объект) вместо `GCodeText`; рукописный парсер G-кода удалён (решение: оставить ли как утилиту для будущего импорта чужих файлов).
- [ ] **6.2** `TrajectoryScene` — чистые данные сцены (сегменты: тип движения, точки, радиус дуги); `SceneRenderer` (слой Views) превращает сцену в `Model3DGroup`.
- [ ] **6.3** 2D: генерация точек контуров — в Core через существующие `IProfileGeometry`/`IPocketGeometry` (убрать «одноразовые» `Profile*Operation` из code-behind); `OperationsPreviewView` получает `OperationScene` из нового `OperationsPreviewViewModel`; code-behind — только отрисовка и мышь.
- [ ] **6.4** Тесты: юнит-тесты `SceneBuilder` (сегменты из `GCodeProgram`), дифференциально со старым парсером на эталонных программах (включая программы с новыми стратегиями фазы 5).

**DoD фазы 6:** ☐ в `PreviewViewModel` и code-behind нет парсеров G-кода и построителей геометрии; ☐ VM не ссылается на `System.Windows.Media.*`; ☐ визуальное совпадение 2D/3D со старым (ручная проверка).

---

## Фаза 7. Представление: MVVM-гигиена (6–10 дней)

**Цель:** VM без WPF, единый источник истины по операциям, базовый класс диалогов. (Замена фреймворка MVVM выполнена в фазе 1; `IDialogService` — там же, п. 1.3.)

- [ ] **7.1** Убрать `Application.Current.Dispatcher` из всех 15+ VM (прямой вызов; проверить каждый, тест + ручной прогон).
- [ ] **7.2** Единая коллекция операций: `MainViewModel.AllOperations` — единственный источник; у операции — `Category` (Drill/Profile/Pocket); под-VM/UI — фильтрованные представления. Удалить: ручную синхронизацию `CollectionChanged` ×3, ручные `Add/Remove` в под-VM, свойство `MainViewModel` в дочерних VM (вверх — через команды/события), switch `AddOperationToCollections`.
  - ⚠️ Видимое изменение: полный прогон чек-листа §8.
- [ ] **7.3** `OperationEditorViewModelBase<T>` + `IOperationEditorFactory` (реестр `Type операции → фабрика VM диалога`); диалоги с явными **OK/Cancel** (OK — валидация + сохранение; Cancel — без изменений; `OnClosed` больше не сохраняет).
  - ⚠️ Видимое UX-изменение.
- [ ] **7.4** Консолидация 15 VM операций на базовый класс (по одному за коммит, golden + smoke после каждого).
- [ ] **7.5** Убрать статик: `PlatformVariables` → IoC (версия, `ILocalizationManager`); `GCodeSettingsStore.Current` → экземпляр `ISettingsStore` из IoC (статический фасад `[Obsolete]` на один релиз); `ThemeHelper` → `IThemeService`.
- [ ] **7.6** `SimpleGCodeGenerator` и всё остальное — только через IoC (убрать `new` из VM).

**DoD фазы 7:** ☐ в `ViewModels/` нет `System.Windows.*` (grep-гейт в CI); ☐ циклические ссылки VM убраны; ☐ чек-лист §8 зелёный; ☐ все golden зелёные.

---

## Фаза 8. Персистентность, настройки, локализация (3–5 дней)

**Цель:** аккуратные настройки, честная локализация, отзывчивый UI. (Сериализация `.ygc` на System.Text.Json + легаси-ридер выполнены в фазе 1, п. 1.2.)

- [ ] **8.1** Разделить `GCodeSettings`: `GCodeFormatSettings`, `SpindleSettings`, `CoolantSettings`, `WorkCoordinateSettings` + `UiSettings` (тема); маппинг `Properties.Settings` — одна таблица вместо ручной копии ×2.
- [ ] **8.2** (D4) Секции spindle/coolant в `.ygc` (обязательно): при сохранении проекта настройки шпинделя/СОЖ пишутся в файл; при открытии — подставляются в сессию; нет секции (старые файлы) → глобальные настройки из `Properties.Settings`. Тесты: старый файл без секций, новый файл с секциями, переоткрытие.
- [ ] **8.3** Локализация: resx на культуру (RU + заготовка EN); убрать захардкоженные fallback'ы из кода (пустота → лог + ключ); имена операций — через ключ, а не значение; `?key?` → логирование + сам ключ.
- [ ] **8.4** async/await: импорт DXF и генерация G-кода — `async` с прогрессом (UI не блокируется); в Core — чистые синхронные методы.

**DoD фазы 8:** ☐ старые `.ygc` открываются (тесты на эталонах), новые — в новой схеме с секциями spindle/coolant; ☐ `GCodeSettingsStore` < 100 строк; ☐ в VM нет захардкоженных строк (grep); ☐ большой DXF (>10k сегментов) не блокирует UI.

---

## 8. Ручной smoke-чек-лист (прогонять после каждого PR фаз 1–8)

Прогон: __________ Дата: __________ Итог: ☐ зелёный / ☐ есть проблемы (описать ниже)

- [ ] Создать по одной операции каждого вида (9 сверл, 6 профилей, 4 кармана) → параметры → OK.
- [ ] Переименовать операцию → двойной клик → открывается **верный** диалог.
- [ ] Изменить порядок операций (вверх/вниз), удалить, отключить/включить (`IsEnabled`) → 2D-превью обновляется.
- [ ] 2D: зум колесом, панорама, hover, выбор кликом, «показать всё».
- [ ] Сгенерировать G-код → сравнить с эталоном (diff) → сохранить `.nc`.
- [ ] 3D-превью: форма траекторий совпадает со старым (эталонные программы), вращение/зум.
- [ ] Карманы: каждая стратегия (Spiral/Concentric/Radial/ZigZag/Lines) + roughing/finishing → корректный G-код и превью.
- [ ] Сохранить проект → выйти → открыть → все операции/параметры/порядок/настройки шпинделя и СОЖ сохранены.
- [ ] Открыть **старый** `.ygc` (до рефакторинга) → всё работает.
- [ ] Настройки: все переключатели (линейные номера, padded G, дуги, шпиндель, СОЖ, WCS, G92) → G-код меняется ожидаемо.
- [ ] Тема светлая/тёмная → переключение без артефактов, 2D-превью перерисовывается.
- [ ] Импорт DXF (профиль и карман): валидный файл, файл без контуров, битый файл → корректные сообщения.

Примечания:

---

## Журнал прогона smoke-чек-листа

| Дата | Фаза/PR | Итог | Примечания |
|------|---------|------|------------|
|      |         |      |            |

---

## Сводка оценок

| Фаза | Содержание | Оценка | Зависимости |
|------|-----------|--------|-------------|
| 0 | Тесты, CI, golden-файлы | 3–5 дн. (готово: 0.1–0.8, 2026-08-22) | — |
| 1 | Миграция на .NET 10: SDK-style, System.Text.Json, CommunityToolkit.Mvvm, MahApps 2.x, TFM (D3, D5) | 8–12 дн. (готово: 1.1–1.6, 2026-08-22/23) | 0 |
| 2 | Выделение Core | 3–5 дн. (готово: 2.1–2.3, 2026-08-23) | 1 |
| 3 | Модель: убить `Metadata`, name-dispatch, валидация | 5–8 дн. | 2 |
| 4 | Структурный G-код, форматтер, реестр, декомпозиция генератора | 8–12 дн. | 3 |
| 5 | Стратегии карманов + roughing/finishing (D1) | 8–12 дн. | 4 |
| 6 | Превью 2D/3D без ре-парсинга, WPF вне VM | 5–7 дн. | 4 |
| 7 | MVVM: единая коллекция, базовый класс диалогов, OK/Cancel, статик | 6–10 дн. | 3–4 |
| 8 | Настройки в `.ygc` (D4), локализация, async | 3–5 дн. | 3, 7 |

**Итого фазы 0–8: ~49–76 рабочих дней (≈ 2,5–3,5 месяца)** работы одного разработчика; каждая фаза заканчивается релизируемым состоянием (сборка + тесты + smoke-чек-лист зелёные).

using System.Runtime.CompilerServices;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows;

// Свойства файла — издатель, продукт, описание, правообладатель — заданы
// в Directory.Build.props, и атрибуты из них генерирует SDK; дублировать их
// здесь нельзя (CS0579). Прежде описание и правообладатель объявлялись тут
// пустой строкой и годом, разошедшимся с лицензией, а издателя не задавал
// никто — в свойствах файла на его месте оказывалось имя сборки.
//
// Пустые AssemblyTrademark и AssemblyCulture удалены вместе с ними: первый
// ничего не утверждал, второй объявлял сборку нейтральной по культуре, что
// SDK делает и без него.

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// Язык нейтрального набора ресурсов — английский: LocalizableResources.resx
// содержит английские строки, русские лежат в LocalizableResources.ru.resx.
// Прежде было наоборот, и это делало русский языком по умолчанию для любой
// системы, у которой нет своего перевода: программа, собранная для станка
// в другой стране, показывала русский интерфейс, а английский считался
// дополнением. Атрибут говорит среде выполнения, что для английской культуры
// сателлит искать не нужно — строки уже в сборке.
[assembly: NeutralResourcesLanguage("en")]


// Доступ к internal-членам (парсеры DXF) из тестового проекта (пункт 0.3 плана).
[assembly: InternalsVisibleTo("GCodeGenerator.Tests")]

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
                                     //(used if a resource is not found in the page,
                                     // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
                                              //(used if a resource is not found in the page,
                                              // app, or any theme specific resource dictionaries)
)]


// Версия (AssemblyVersion/FileVersion/InformationalVersion) генерируется SDK
// из git-тега при сборке (Directory.Build.targets + build/Get-GitVersion.ps1);
// в этом файле — только не-версионные атрибуты (ThemeInfo, InternalsVisibleTo).

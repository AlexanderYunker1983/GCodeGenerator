using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
// AssemblyTitle/AssemblyCompany/AssemblyProduct/AssemblyConfiguration генерирует
// SDK (из AssemblyName/Configuration) — дублировать их здесь нельзя (CS0579).
[assembly: AssemblyDescription("")]
[assembly: AssemblyCopyright("Copyright ©  2025")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

//In order to begin building localizable applications, set
//<UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file
//inside a <PropertyGroup>.  For example, if you are using US english
//in your source files, set the <UICulture> to en-US.  Then uncomment
//the NeutralResourceLanguage attribute below.  Update the "en-US" in
//the line below to match the UICulture setting in the project file.

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


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

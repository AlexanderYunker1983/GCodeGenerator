; ---------------------------------------------------------------------------
; GCodeGenerator.iss - Inno Setup 7.x script for the GCodeGenerator app.
;
; The version is NOT hardcoded here - it is passed on the ISCC command line:
;   /DAppVersionNumeric=0.0.1  /DAppVersionSuffix=-rc5
; build/Make-Installer.ps1 computes both from an exact git tag or the planned
; build/NEXT_VERSION (the same mechanism that stamps the assembly version).
; The #define values below are fallbacks for a local compile without /D;
; #ifndef keeps the command-line /D values in effect (a plain #define in the
; script would OVERRIDE them).
;
; Compile:
;   ISCC.exe /DAppVersionNumeric=0.0.1 /DAppVersionSuffix=-rc5 /O<out> install\GCodeGenerator.iss
; (ISCC /O syntax: the path is appended directly, no '=' or '+').
; or simply:
;   build\Make-Installer.ps1
;
; Notes:
;   - AppId is a FIXED GUID: never change it. It is the upgrade key - a new
;     AppId would install alongside the old version instead of upgrading.
;   - The app is a SELF-CONTAINED win-x64 publish (build/Make-Installer.ps1
;     default): the installer ships the .NET 10 Desktop Runtime, so end users
;     need nothing preinstalled. Windows Restart Manager identifies the exact
;     process using files under {app}, asks it to close and starts it again.
;   - Code signing is optional and configured from outside: the ISCC command
;     line carries /DSignToolName + /S<name>=<command> when a certificate is
;     available. Without them the script compiles an unsigned installer, as
;     it always did.
;   - The install mode is chosen by the person installing: all users (needs
;     elevation) or just me (does not). Every path and registry root follows
;     that choice through the "auto" constants and HKA.
;   - This file is UTF-8 WITH BOM: [CustomMessages] carries Russian text, and
;     Inno Setup reads a BOM-less .iss as ANSI.
;   - Inno Setup 7.0+ is required for a native 64-bit Setup executable.
;     The official Russian translation is Languages\Russian.isl.
; ---------------------------------------------------------------------------

; Publisher, product name and copyright come from Directory.Build.props via
; build/Make-Installer.ps1 - the same values the assemblies carry in their file
; properties. Keeping a second copy here is how they drifted apart before: the
; publisher was spelled without a space and the copyright year disagreed with
; the license. The fallbacks below only apply to a bare ISCC run without /D.
#ifndef AppPublisher
  #define AppPublisher "Alexander Yunker"
#endif
#ifndef AppProductName
  #define AppProductName "GCodeGenerator"
#endif
#ifndef AppCopyright
  #define AppCopyright "Copyright (c) 2021-2026 Alexander Yunker"
#endif

#ifndef AppVersionNumeric
  #define AppVersionNumeric "0.0.1"
#endif
#ifndef AppVersionSuffix
  #define AppVersionSuffix ""
#endif

[Setup]
; AppId - fixed GUID for upgrades (see header note).
AppId={{BC1D74F7-289B-4721-951C-1B4885EA215E}
AppName={#AppProductName}
AppPublisher={#AppPublisher}
AppCopyright={#AppCopyright}
LicenseFile=..\LICENSE
; AppVersion = the full git tag (e.g. 0.0.1-rc5): displayed in the wizard
; and written to the uninstall registry (DisplayVersion, a string).
; Inno Setup has no separate suffix directive - the full tag is the display
; version; the numeric part is used for the PE version resource below.
AppVersion={#AppVersionNumeric}{#AppVersionSuffix}
DefaultDirName={autopf}\GCodeGenerator
DefaultGroupName=GCodeGenerator
DisableProgramGroupPage=yes
; 64-bit app (win-x64 publish): install on x64 only, into 64-bit Program Files.
; A native x64 Setup avoids a WOW64 dependency and enables high-entropy ASLR.
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Minimum supported OS per README: Windows 11 24H2.
; Windows 10 is outside both its own lifecycle and the .NET 10 support list.
MinVersion=10.0.26100
; Установка возможна и без прав администратора.
;
; Прежде мастер требовал их безусловно, и на рабочем компьютере с
; ограниченной учётной записью программу поставить было нельзя вовсе —
; при том что она никакой части системы не касается: каталог, ярлыки и
; связь с файлами у неё свои.
;
; lowest + dialog: мастер запускается без повышения прав и спрашивает,
; ставить для всех пользователей или только для себя. Первое запрашивает
; повышение, второе обходится без него и кладёт программу в
; %LOCALAPPDATA%\Programs. Каталог, ярлыки, группа меню и ветка реестра
; выбираются режимом установки сами ({autopf}, {autodesktop}, {group}, HKA),
; поэтому ни одну из них не пришлось раздваивать.
;
; commandline — те же два режима ключами /ALLUSERS и /CURRENTUSER: тихая
; установка не может отвечать на вопрос мастера.
;
; UsePreviousPrivileges (умолчание — да) избавляет от вопроса тех, у кого
; программа уже стоит: обновление идёт в том же режиме, что и установка,
; и второй копии рядом с первой не появляется.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
; Tells the shell that file associations changed, so the new icon and the
; "open with" entry appear without a sign-out.
ChangesAssociations=yes
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; Path-aware Windows Restart Manager replaces the former search by image
; name. It never force-kills an unrelated portable copy and
; lets the WPF Closing handler ask whether to save the open project.
CloseApplications=yes
RestartApplications=yes
; Two languages, auto-selected by system locale (no language dialog).
ShowLanguageDialog=no
SetupIconFile=..\GCodeGenerator\cnc_machine.ico
UninstallDisplayIcon={app}\GCodeGenerator.exe
OutputBaseFilename=GCodeGenerator-Setup-{#AppVersionNumeric}{#AppVersionSuffix}
; VersionInfo* - 4-part numeric file version (the tag suffix is not allowed
; in the PE version resource; the full tag is visible in the wizard and the
; app's own version string).
VersionInfoVersion={#AppVersionNumeric}.0
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright={#AppCopyright}
VersionInfoProductName={#AppProductName}
VersionInfoDescription={#AppProductName} installer

; Code signing. The signing command is NOT stored here: it is passed on the
; ISCC command line by build/Make-Installer.ps1 when signing is configured
;   /DSignToolName=<name>  /S<name>="<command with $f for the file>"
; and omitted otherwise, so an unsigned build still compiles unchanged.
; SignedUninstaller signs the uninstaller too - it is generated at install
; time from a stub inside Setup, and an unsigned stub would leave one
; unsigned executable on the user's machine.
#ifdef SignToolName
SignTool={#SignToolName}
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

; Own messages of the [Code] section. The wizard itself picks its language by
; system locale; these lines used to be English-only, so a Russian user met
; the only questions the installer actually asks in a foreign language.
; This file is UTF-8 with BOM - required by Inno Setup for non-ASCII text.
[CustomMessages]
english.AssociationsGroup=File associations:
russian.AssociationsGroup=Связь с файлами:
english.AssociateProjectFiles=Open .ygc project files with GCodeGenerator
russian.AssociateProjectFiles=Открывать файлы проектов .ygc в GCodeGenerator
english.ProjectFileTypeName=GCodeGenerator project
russian.ProjectFileTypeName=Проект GCodeGenerator

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
; Association is a task, not a given: .ygc is this product's own format, but
; the person installing it may already have the extension bound elsewhere.
Name: "associate"; Description: "{cm:AssociateProjectFiles}"; GroupDescription: "{cm:AssociationsGroup}"

[Registry]
; File association for .ygc, written only when the task above is selected.
; Root HKA follows the install mode: HKLM for an all-users install, HKCU for
; a per-user one. The ProgId key is deleted on uninstall; the extension key
; only loses its own value, so an association set by another program survives.
Root: HKA; Subkey: "Software\Classes\.ygc"; ValueType: string; ValueName: ""; ValueData: "GCodeGenerator.Project"; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Classes\GCodeGenerator.Project"; ValueType: string; ValueName: ""; ValueData: "{cm:ProjectFileTypeName}"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Classes\GCodeGenerator.Project\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\GCodeGenerator.exe,0"; Tasks: associate
; %1 in quotes: a project path with spaces must arrive as one argument.
Root: HKA; Subkey: "Software\Classes\GCodeGenerator.Project\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\GCodeGenerator.exe"" ""%1"""; Tasks: associate

[Files]
; Publish output (build/Make-Installer.ps1: dotnet publish -o
; artifacts\publish\GCodeGenerator). Recurses into the de/en satellite dirs.
Source: "..\artifacts\publish\GCodeGenerator\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\GCodeGenerator"; Filename: "{app}\GCodeGenerator.exe"
Name: "{group}\{cm:UninstallProgram,GCodeGenerator}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GCodeGenerator"; Filename: "{app}\GCodeGenerator.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GCodeGenerator.exe"; Description: "{cm:LaunchProgram,GCodeGenerator}"; Flags: nowait postinstall skipifsilent unchecked

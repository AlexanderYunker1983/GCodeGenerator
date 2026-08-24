; ---------------------------------------------------------------------------
; GCodeGenerator.iss - Inno Setup 6.x script for the GCodeGenerator app.
;
; The version is NOT hardcoded here - it is passed on the ISCC command line:
;   /DAppVersionNumeric=0.0.1  /DAppVersionSuffix=-rc5
; build/Make-Installer.ps1 computes both from the git tag (the same
; build/Get-GitVersion.ps1 mechanism that stamps the assembly version).
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
;     need nothing preinstalled. The installer also closes a running app
;     instance (PrepareToInstall).
;   - Inno Setup 6.3+ (x64compatible values); Russian: official in 6.7+
;     (Languages\Russian.isl), unofficial in older 6.x (Languages\Unofficial).
; ---------------------------------------------------------------------------

#ifndef AppVersionNumeric
  #define AppVersionNumeric "0.0.1"
#endif
#ifndef AppVersionSuffix
  #define AppVersionSuffix ""
#endif

[Setup]
; AppId - fixed GUID for upgrades (see header note).
AppId={{BC1D74F7-289B-4721-951C-1B4885EA215E}
AppName=GCodeGenerator
AppPublisher=AlexanderYunker
; AppVersion = the full git tag (e.g. 0.0.1-rc5): displayed in the wizard
; and written to the uninstall registry (DisplayVersion, a string).
; Inno Setup 6 has no separate suffix directive - the full tag is the display
; version; the numeric part is used for the PE version resource below.
AppVersion={#AppVersionNumeric}{#AppVersionSuffix}
DefaultDirName={autopf}\GCodeGenerator
DefaultGroupName=GCodeGenerator
DisableProgramGroupPage=yes
; 64-bit app (win-x64 publish): install on x64 only, into 64-bit Program Files.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Minimum OS per README (Windows 10 22H2 / Windows 11).
MinVersion=10.0.19045
PrivilegesRequired=admin
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; Two languages, auto-selected by system locale (no language dialog).
ShowLanguageDialog=no
SetupIconFile=..\GCodeGenerator\cnc_machine.ico
UninstallDisplayIcon={app}\GCodeGenerator.exe
OutputBaseFilename=GCodeGenerator-Setup-{#AppVersionNumeric}{#AppVersionSuffix}
; VersionInfo* - 4-part numeric file version (the tag suffix is not allowed
; in the PE version resource; the full tag is visible in the wizard and the
; app's own version string).
VersionInfoVersion={#AppVersionNumeric}.0
VersionInfoCompany=AlexanderYunker
VersionInfoDescription=GCodeGenerator installer

; Russian: official in Inno Setup 6.7+, unofficial (Languages\Unofficial) in
; older 6.x - pick whichever exists.
#if FileExists("compiler:Languages\Russian.isl")
  #define RussianIsl "compiler:Languages\Russian.isl"
#else
  #define RussianIsl "compiler:Languages\Unofficial\Russian.isl"
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "{#RussianIsl}"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Publish output (build/Make-Installer.ps1: dotnet publish -o
; artifacts\publish\GCodeGenerator). Recurses into the de/en satellite dirs.
Source: "..\artifacts\publish\GCodeGenerator\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\GCodeGenerator"; Filename: "{app}\GCodeGenerator.exe"
Name: "{group}\{cm:UninstallProgram,GCodeGenerator}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GCodeGenerator"; Filename: "{app}\GCodeGenerator.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GCodeGenerator.exe"; Description: "{cm:LaunchProgram,GCodeGenerator}"; Flags: nowait postinstall skipifsilent

[Code]
// NB: in [Code] ';' is a statement separator, NOT a comment (use //; the
// ';' comment style only works in the directive sections above).
//
// --- Close a running app instance ------------------------------------------
// Detection via tasklist (Inno Pascal has no Pointer type, so native struct
// enumeration is out): the /FI filter is an exact image-name match, so the
// output contains the name only when the app is running (locale-independent -
// the "no tasks" message never contains the image name).
function IsAppRunning: Boolean;
var
  TempFile: String;
  Content: AnsiString; // LoadStringFromFile takes var AnsiString
  ResultCode: Integer;
begin
  TempFile := ExpandConstant('{tmp}\gcodegen_tasklist.txt');
  Result := False;
  if Exec('cmd.exe', '/c tasklist.exe /FI "IMAGENAME eq GCodeGenerator.exe" /NH > "' + TempFile + '"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
  then
  begin
    if LoadStringFromFile(TempFile, Content) then
      Result := Pos('GCodeGenerator.exe', Content) > 0;
  end;
  DeleteFile(TempFile);
end;

// PrepareToInstall: a non-empty return string stops Setup on the
// "Preparing to Install" page and is shown as the error message.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  Answer: Integer;
begin
  Result := '';
  if IsAppRunning then
  begin
    Answer := MsgBox('GCodeGenerator is running. Close it and continue the installation?',
                     mbConfirmation, MB_YESNO);
    if Answer = IDYES then
    begin
      Exec('taskkill.exe', '/F /IM GCodeGenerator.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
      Sleep(500);
    end;
    if IsAppRunning then
      Result := 'GCodeGenerator is running and could not be closed. Close it and run the installer again.';
  end;
end;

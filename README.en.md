# GCodeGenerator

[Русский](README.md) | **English**

A simple G-code generator for CNC machines, with a graphical interface and 3D toolpath visualization.

Ready-made builds are on the [Releases](https://github.com/AlexanderYunker1983/GCodeGenerator/releases) page; the installed version is shown in the window title. Next up is version 2.x: cross-platform support and more interactive work with 2D and 3D.

## Description

GCodeGenerator is a Windows application for quickly creating G-code programs for CNC machines through a convenient graphical interface, without being tied to a CAD system. It supports several kinds of operations (drilling, milling) and includes a 3D preview of the toolpath.

## Features

### Drilling operations
- **Drilling at points** — holes at the given coordinates
- **Drilling along a line** — holes spaced evenly along a line
- **Drilling in an array** — a grid of holes
- **Drilling along a rectangle** — holes around the perimeter of a rectangle
- **Drilling along a circle** — holes around a circle
- **Drilling along an ellipse** — holes around an ellipse, rotation supported
- **Drilling from a package** — predefined hole patterns

### Profile milling
- **Rectangle** — the contour of a rectangle, rotation supported
- **Rounded rectangle** — a contour with corner radii
- **Circle** — the contour of a circle
- **Ellipse** — the contour of an ellipse, rotation supported
- **Polygon** — the contour of a regular polygon (triangle, square, hexagon and so on)
- **DXF import for contours** — contours from DXF files: lines, arcs, circles, ellipses and polylines (including arc segments), geometry inside inserted blocks, coordinates converted according to the drawing units

### Pocket milling
- **Rectangular pocket** — the contour of a rectangle with material removal, rotation supported
- **Circular pocket** — the contour of a circle with material removal
- **Elliptical pocket** — the contour of an ellipse with material removal, rotation supported
- **DXF import for pockets** — closed contours from DXF files

**Pocketing strategies** (available for every kind of pocket, DXF included):
- **Spiral** — one continuous spiral filling the pocket
- **Concentric** — nested closed passes along the offset contour
- **Radial** — passes ("spokes") from the centre towards the contour
- **Zigzag** — serpentine passes at a given angle, alternating direction on every line
- **Lines** — parallel passes at a given angle; every section of the cut is a separate cut with a retract (islands and gaps are handled)

**Roughing and finishing with an allowance**:
- **Roughing** — material removal leaving an allowance for finishing (on the contour and on the depth)
- **Finishing** — the final pass, in one of three modes:
  - **Walls** — finishing the walls (the cutter edge on the wall)
  - **Floor** — finishing the floor
  - **All** — both walls and floor

### Other features
- **3D toolpath visualization** — an interactive view of the tool movements. The scene is built in the background; if it cannot be built, the window says so in place of the scene and names the reason — the program itself is ready, it can be viewed as text and saved. The details of the failure go to the application log
- **G-code preview** — the generated program before saving, as code and as a 2D view of the operations. The 2D view switches between operation contours and the toolpath: the toolpath shows what the machine will actually do — the removal passes, the cutter radius compensation and the rapid moves (dashed)
- **Safe program preamble** — before the first move the program states the machine's modal state: millimetres (G21), absolute coordinates (G90), the XY plane (G17), feed per minute (G94), cutter radius and tool length compensation off and canned cycle cancelled (G40/G49/G80). Without them the result would depend on whichever program ran on the controller before this one
- **English-only comments** — a comment goes not to the user but into a file read by the controller: many controllers accept Latin characters only, and on Cyrillic they either refuse the block or show garbage — which you find out at the machine. The program's own texts are English, and a Russian operation name is not emitted at all: the comment keeps the English description of the operation, with its type and dimensions. English names are emitted as they are
- **Safe height validation** — the heights at which the tool travels above the workpiece are checked against the workpiece itself, not merely against being a number: the safe milling height must be above the contour height, the height between holes above the highest of them, and the retract inside a hole not below its top. All three are absolute, so the default value — safe above zero — drives the tool into the material when machining a boss. The error is shown next to the field and clears when any of the linked heights is corrected
- **Sanity ceilings for feeds and spindle speed** — feed, spindle speed and spindle-start delay have a general upper ceiling as well as a lower bound: 20,000 mm/min for cutting feed, 60,000 mm/min for rapid feed and 60,000 rpm for the spindle. This catches plainly absurd values; it is not a machine profile. For example, 3,000 mm/min may be either a mistyped 300 or a legitimate cutting mode, so the application cannot reject it without knowing the equipment. The operator must compare feeds and speed with the machine data sheet before running the program; when a general ceiling is exceeded, the error message names it
- **Local machine profile** — optional validation of the actual toolpath against configured X/Y/Z bounds, work and rapid feeds, and spindle speed. Cutter compensation and intermediate arc extrema are included, and an over-limit path cannot be generated. The profile stays on the computer and is never taken from an opened project, so a foreign file cannot weaken it. Coordinates are program coordinates; the operator must still verify the controller's work offset
- **Peck return with a clearance** — the retract height between passes is absolute, so the drill usually leaves the hole completely and has to travel back down its whole depth. The rapid move now stops half a millimetre above the depth already drilled, and the last stretch is done at the working feed: the chips the retract is made for are always there, and meeting them at full speed has nothing to gain. The return never rises above the top of the hole — there is no material there
- **Plunging at the working feed only** — a rapid move lowers the tool no deeper than the top of the layer being machined: above it there is no material left. Everything below is done at the Z plunge feed. The rule holds where a pass starts somewhere other than the layer entry point as well: a separate cut of a parallel line, a section of a spoke or contour behind an island, a spiral turn broken by an island, a wall finishing pass
- **Generation settings** — the output format of the G-code, including the choice of controller (post-processor): Generic (Fanuc-compatible) or GRBL / LinuxCNC. The choice is stored in the project file, so a project is always generated for the controller it was made for. Generation settings belong to the open project; the "Save as defaults" button in the settings window remembers them as the defaults for new projects
- **Spindle settings** — spindle control in the program: on/off (M3/M4/M5), speed (the S word), direction of rotation, delay after starting. The delay is entered in seconds and emitted in the units of the chosen controller: `G4 P<milliseconds>` for Generic (that is how Fanuc and compatible controllers read the argument) and `G4 P<seconds>` for GRBL / LinuxCNC
- **Coolant settings** — coolant control from the program: on/off (M8/M9)
- **Saving and opening projects** — all operations and settings are stored in a project file to be loaded later and continued. A project opens in three ways: from the application window, by double-clicking a `.ygc` file in Explorer (the file association is offered during installation) and by dragging a file into the window. Unsaved changes are asked about in all three
- **Interface language** — Russian and English; chosen in the settings and applied at once, without a restart. The system language is used by default, and English when there is no translation for it. Failures speak the interface language too: the reason a program was not built is listed one problem per line — the number and name of the operation, the name of the parameter and what is wrong with it, in the same words the operation dialogs use. The English text of the failure stays in the application log
- **Operation management** — adding, removing and reordering operations; undo and redo of both those changes and parameter edits (Ctrl+Z / Ctrl+Y), one history step per dialog. The history keeps the last 100 steps: every step holds the whole state of an operation, and for a contour imported from a drawing that is hundreds of kilobytes
- **Cancelling generation** — a stop button appears next to the progress bar: on a large project with a fine stepover there is no need to wait for the build to finish once it is clear the parameters are wrong. Stopping takes a fraction of a second — it reaches every layer and every hole
- **Document shortcuts** — Ctrl+N (new project), Ctrl+O (open), Ctrl+S (save), Ctrl+Shift+S (save as), F1 (about). The shortcut is named in the tooltip of every toolbar button
- **Update check** — optional and off by default. This is the only time the application goes online: it asks github.com for the latest release and nothing else — the request needs no keys and no credentials. Turn it on in the settings and a "Version X is available" line appears at startup, opening the release page. The "Check now" button in the About window works regardless of the setting: pressing it is the consent. A failed check interrupts nothing and names itself: the window says what got in the way — the network, GitHub’s answer, or the request limit it applies, which lifts on its own
- **About window** — version, copyright holder, licence, product page and the path to the application log with a "show file" button: the log is what a bug report is asked for, and it should not have to be hunted for inside the profile
- **Application log** — opening and saving projects, generating G-code, importing DXF and any failure are written to `%LOCALAPPDATA%\GCodeGenerator\logs\gcodegenerator.log` (at 1 MB the file is rolled over to `gcodegenerator.1.log`)
- **Project auto-recovery** — two seconds after the last edit, the unsaved document is written atomically to `%LOCALAPPDATA%\GCodeGenerator\recovery\autosave.ygc`; after an interrupted session, the next regular launch offers to open it as a new unsaved project. A successful manual save or an intentional discard removes the snapshot
- **Crash snapshot** — if the application is terminated by an unexpected error, the current project is saved to `%LOCALAPPDATA%\GCodeGenerator\crash\crash-YYYYMMDD-HHMMSS.ygc` — as a separate file, not over yours; the path is shown in the error message

## Limits of the current version

Everything below is a deliberate boundary of the first version, not an
unfinished corner. Better to know about them in advance than to find out at
the machine.

- **Millimetres only.** The program preamble emits `G21`; the inch mode
  (`G20`) is supported neither on input nor on output.
- **Three linear axes and the XY plane.** `G17` is emitted; there is no
  machining in other planes and no rotary axes.
- **Two controllers.** Generic (Fanuc-compatible) and GRBL / LinuxCNC. They
  differ in the unit of the delay after the spindle starts — `G4 P` in
  milliseconds and in seconds — and in nothing else.
- **No canned cycles.** Drilling is emitted as explicit `G0`/`G1` moves
  rather than `G81`/`G83`: the program then runs the same way on controllers
  that read the cycles differently, and it is visible in full in the preview.
- **Cutter compensation is computed by the program, not by the controller.**
  The contour is offset by the cutter radius while the toolpath is built, and
  `G40` is emitted. `G41`/`G42` are never emitted, and the controller's tool
  table has no effect.
- **One tool per program.** There is no tool change (`T`/`M6`) and no
  subprograms (`M98`/`M99`): a second tool means a second program.
- **The spindle and the coolant are simple commands.** Speed, direction and
  delay (`M3`/`M4`/`M5`, `S`, `G4`), coolant on and off (`M8`/`M9`). There is
  no spindle orientation and no separate coolant control (`M7`).
- **Input limits.** The working feed is capped at 20000 mm/min, the rapid at
  60000 mm/min, the spindle at 60000 rpm, the delay at 60 s and the decimals
  at 6. These are the bounds of sensible input, not the machine's data sheet:
  the controller applies its own limit anyway.
- **A single job has size limits.** `.ygc` and DXF files may be at most 64 MB;
  the bound is checked before a file is read into memory. A project may have
  up to 1000 operations; an operation up to 10000 holes; a DXF import up to
  200000 entities, 64 nested block levels, 10000 contours and 200000 points;
  and the resulting toolpath up to 250000 items. These bounds protect the
  application's memory and responsiveness; a larger job must be split into
  several projects or drawings.
- **The peck return clearance is 0.5 mm** and is not configurable.
- **The undo history holds 100 steps.**
- **Project backup.** When an existing `.ygc` is overwritten, its previous version is atomically kept next to it with a `.bak` suffix; rename the copy back to `.ygc` to recover it.
- **Foreign G-code is not opened.** The application writes its files but does
  not read them: the preview shows the toolpath it built itself.
- **DXF drawings are not read in full.** Lines, arcs, circles, ellipses,
  polylines (3D ones and ones with arc segments included), splines
  (approximated by a polyline) and block inserts are taken. Text, dimensions,
  hatches and the rest do not count as contour geometry.
- **Windows only.** The interface is written in WPF; cross-platform support is
  promised for version 2.x.

## Requirements

- **Operating system: Windows 11 24H2 (build 26100) or newer.** Windows 10
  reached end of support and is absent from the
  [operating systems supported by .NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md),
  so the application is not supported there; the installer enforces the same
  minimum build 26100.
- **Display:** the window takes at least 900×560 layout units — that is 1600×1000 pixels at 175 % scaling, or 900×560 at 100 %. On 1920×1080 the application works at any scaling up to 175 %.
- The **.NET 10 Desktop Runtime** is **not required** to run the application: the installer and the portable build are self-contained (the runtime is included) — the application runs on a clean Windows.
- To build from source: the **.NET 10 SDK** (10.0.x) — Visual Studio 2026 or the command line.

## Installation

### Ready-made builds
Download the latest installer from the [Releases](https://github.com/AlexanderYunker1983/GCodeGenerator/releases) page and run it.

The wizard asks whether to install for all users or for the current user only. The first needs administrator rights, the second does not and puts the application into `%LOCALAPPDATA%\Programs` — on a work computer with a restricted account the application can still be installed. The install directory, the shortcuts and the file association all follow the chosen mode. A silent install picks the mode with `/ALLUSERS` or `/CURRENTUSER`. If the application is already installed, the question is not asked: an update keeps the mode of the installation.

Releases are published automatically: pushing a version tag (`1.2.3` or `1.2.3-rc5`) starts the [Release](.github/workflows/release.yml) workflow — build, tests, installer (Inno Setup) and a GitHub Release with `GCodeGenerator-Setup-<version>.exe` and a portable build (zip). Both artifacts are self-contained (they include the .NET 10 Desktop Runtime). Tags with a suffix (`-alpha`/`-beta`/`-rc`) are marked as pre-releases. An official release is published only after its draft is complete and then becomes immutable: its tag and assets cannot be replaced.

**The Windows warning on first run.** Official builds do not yet carry an Authenticode signature and therefore show an unknown publisher. Depending on file reputation and the computer's policy, SmartScreen or Smart App Control may display a warning or block the file completely. A missing signature proves neither infection nor safety. Download the application only from this repository's Releases page and do not disable system protection. If Windows policy offers “More info” → “Run anyway”, decide whether to proceed only after verifying the file.

`SHA256SUMS.txt` is published next to the artifacts. This command checks that the local installer matches its entry:

```powershell
$version = '<version>'
$file = "GCodeGenerator-Setup-$version.exe"
$line = @(Get-Content .\SHA256SUMS.txt | Where-Object {
    $_ -match ('  ' + [regex]::Escape($file) + '$')
})
if ($line.Count -ne 1) { throw "There is not exactly one checksum for $file" }
$expected = ($line[0] -split '\s+')[0].ToLowerInvariant()
$actual = (Get-FileHash -LiteralPath ".\$file" -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -cne $expected) { throw "SHA-256 mismatch: $actual" }
```

To verify cryptographically that the release is immutable and that the local file is one of its assets, install the [GitHub CLI](https://cli.github.com/) and run:

```powershell
gh release verify $version --repo AlexanderYunker1983/GCodeGenerator
gh release verify-asset $version ".\$file" --repo AlexanderYunker1983/GCodeGenerator
```

A future signed build will identify the expected publisher on the “Digital Signatures” tab. A valid signature reduces uncertainty about origin, but by itself guarantees neither the absence of warnings nor the safety of the application.

### Updating over a running application

During an update, Windows Restart Manager identifies the installed instance by the files it is using rather than every process with the same name, so a portable copy in another directory is left alone. The application can still ask about an unsaved project; refusing to close stops file replacement without a forced loss of data. After a successful update, the previous instance is started again and reopens the same saved project.

### Building the installer locally

Requirements: the **.NET 10 SDK**, **git** and **64-bit Inno Setup 7** ([download](https://jrsoftware.org/isdl.php)).

```powershell
build\Make-Installer.ps1
```

The script takes the version from an exact git tag or from `build/NEXT_VERSION` for a development build (the same mechanism as the assembly version), runs `dotnet publish` (self-contained, win-x64 — the runtime goes into the installer) and compiles `install\GCodeGenerator.iss` (ISCC). The result is `artifacts\installer\GCodeGenerator-Setup-<version>.exe`.

Parameters: `-FrameworkDependent` (publish without the runtime — a smaller installer, but the user needs the .NET 10 Desktop Runtime), `-IsccPath <path to ISCC.exe>`, `-Configuration`, `-Runtime`, `-SigningMode`, `-SignCommand`, `-ExpectedSignerThumbprint`.

#### Code signing

There is no certificate for the current release, so the official policy is explicit: `-SigningMode Unsigned`. This mode rejects signing parameters and environment variables, preventing a stale secret from changing an artifact accidentally.

```powershell
build\Make-Installer.ps1 -SigningMode Unsigned
```

Once a publicly trusted certificate is available, use the fail-closed `Required` mode. It requires both a command that signs one file, with `$f` replaced by its path, and the expected certificate's SHA-1 thumbprint:

```powershell
build\Make-Installer.ps1 -SigningMode Required `
  -SignCommand 'signtool.exe sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f C:\keys\cert.pfx /p PASSWORD $f' `
  -ExpectedSignerThumbprint '<SHA-1 thumbprint>'
```

The values may instead be put in `GCODEGEN_SIGN_COMMAND` and `GCODEGEN_EXPECTED_SIGNER_THUMBPRINT`; they are read only in `Required` mode. `GCodeGenerator.exe` and both product assemblies in the publish directory are signed, followed by the installer and uninstaller (`SignedUninstaller`); Authenticode trust, the exact expected certificate and a timestamp are verified after every stage. The .NET runtime files are already signed by Microsoft. The active release workflow intentionally receives no signing secrets; connecting a hardware token or cloud key store and switching to `Required` remains a separate task for when a certificate exists.

### Building from source

Requirements: the **.NET 10 SDK** — the exact version is pinned in `global.json`
(10.0.302 or newer of the same band). The package set is fixed by
`packages.lock.json`; to restore exactly by the lock, add `--locked-mode` to
the restore, as the build workflows do. After changing a package version in a
`.csproj`, the lock is updated by restoring with `--force-evaluate`.

The lock covers publishing as well: the application and the core declare their
runtime (`RuntimeIdentifiers` = `win-x64`), so their locks have a section both
for a plain build and for publishing under that runtime. The installer is built
in the same locked mode — the publish is invoked with the
`-p:RestoreLockedMode=true` property (`dotnet publish` has no `--locked-mode`
switch). When updating the locks, restore the whole solution at once
(`dotnet restore GCodeGenerator.sln --force-evaluate`): both sections are
written in a single pass, and a lock with only one of them makes the restore
for the other fail.

1. Clone the repository:
```bash
git clone https://github.com/AlexanderYunker1983/GCodeGenerator.git
cd GCodeGenerator
```

2. Build the solution (NuGet dependencies are restored automatically):
```bash
dotnet build GCodeGenerator.sln -c Release
```

3. (Optional) Run the tests:
```bash
dotnet test GCodeGenerator.Core.Tests/GCodeGenerator.Core.Tests.csproj -c Release --no-build
```
```bash
dotnet test GCodeGenerator.Tests/GCodeGenerator.Tests.csproj -c Release --no-build
```

4. Before changing release-critical validation or generation code, run the
mutation suite (a complete run takes several minutes):
```bash
dotnet tool restore
cd GCodeGenerator.Core.Tests
dotnet stryker --config-file stryker-config.json --skip-version-check
```

The tool version is pinned in `.config/dotnet-tools.json`, and a score below
70% fails the run. The `Mutation Tests` workflow runs the same check weekly or
on demand and retains both HTML and JSON reports as an artifact.

The application: `GCodeGenerator\bin\Release\GCodeGenerator.exe` (running it requires the .NET 10 Desktop Runtime).

Alternatively, open `GCodeGenerator.sln` in Visual Studio 2026 and build the solution (the Release configuration).

### Versioning

The product version is set from git tags and an explicit next-release plan (`build/NEXT_VERSION`, `build/Get-GitVersion.ps1`, `Directory.Build.targets`):

- Tag format: `X.Y.Z` or `X.Y.Z-suffix` (for example `1.2.3`, `1.2.3-alpha`, `1.2.3-rc5`).
- If the current commit carries several tags, the one with the highest precedence wins (SemVer): `1.2.3` > `1.2.3-rc5` > `1.2.3-beta3` > `1.2.3-alpha2` > `1.2.3-alpha` (within a class, by number: `rc10` > `rc5`).
- If the current commit has no tag, its distance and short SHA are appended to
  the version in `build/NEXT_VERSION`, for example `1.2.3-dev4g12ab34cd`; a build with
  uncommitted changes also ends in `dirty`. Two different development builds
  therefore cannot present themselves as the same release.
- `NEXT_VERSION` must contain one valid version newer than the nearest tag;
  a stale or malformed file stops the build. Without an explicitly supplied
  file, the script retains its previous fallback algorithm.
- Tags outside the format (`v1.2.3`, for example) are ignored with a warning.

The version goes into the assembly properties (`Version`/`AssemblyVersion`/`FileVersion`/`InformationalVersion`) and into the window title. To override: `dotnet build /p:Version=1.2.3-rc5` (an explicit version) or `/p:SkipGitVersion=true` (skip git).

## Usage

1. **Start the application**, GCodeGenerator.exe

2. **Pick the kind of operation** from the tabs on the left:
   - Drilling
   - Milling

3. **Add an operation**:
   - Click the button for the kind of operation you need
   - Fill in the parameters in the dialog that opens
   - The operation is added to the list

4. **Adjust the settings** (optional):
   - Click the "Settings" button to change the G-code generation parameters
   - Spindle and coolant parameters are in the same settings window

5. **Save the project** (optional):
   - Click "Save project" to store all operations and settings
   - A saved project can be reopened later through "Open project"

6. **Generate the G-code**:
   - Click "Generate G-code"
   - The result appears in the right-hand panel

7. **Look at the 3D preview** (optional):
   - Click "Preview G-code" to visualize the toolpath

8. **Save the result**:
   - Click "Save G-code"
   - Choose where to write the file

## Project layout

```
GCodeGenerator/
├── GCodeGenerator/          # Application (WPF, net10.0-windows)
│   ├── Views/               # Windows and markup (XAML)
│   ├── ViewModels/          # View models (MVVM)
│   ├── Services/            # Application services (dialogs, settings, clipboard)
│   ├── Localization/        # Binding localization to the markup
│   └── Infrastructure/      # Container, converters, numeric input
├── GCodeGenerator.Core/     # Core without a UI (net10.0)
│   ├── Models/              # Operations, settings, parameter validation
│   ├── Toolpath/            # The toolpath before a dialect is chosen
│   ├── GCodeGenerators/     # Building toolpaths and writing G-code
│   ├── Geometry/            # Plane geometry and offsets
│   ├── Import/              # Reading DXF drawings
│   ├── Preview/             # Flat preview
│   ├── Trajectory/          # The 3D toolpath scene
│   ├── Persistence/         # The project file (.ygc)
│   └── Localization/        # Dictionaries and language selection
├── GCodeGenerator.Core.Tests/  # Core tests (MSTest, no WPF)
├── GCodeGenerator.Tests/    # Application tests (MSTest, WPF)
├── build/                   # Build scripts (tags/NEXT_VERSION, installer)
├── install/                 # Installer (Inno Setup)
├── docs/                    # Documentation (the smoke checklist)
├── GCodeGenerator.sln       # Visual Studio solution
└── LICENSE                  # MIT licence
```

## Technologies

- **.NET 10** — the platform (core — `net10.0`, application — `net10.0-windows`)
- **WPF** — the graphical interface
- **CommunityToolkit.Mvvm** — the MVVM framework
- **MahApps.Metro** — UI themes and controls
- **Autofac** — the dependency container
- **Clipper2** — contour offsets for pocket removal
- **netDxf** — reading DXF drawings
- **NuGet** — dependency management

## Licence

This project is distributed under the MIT licence. See the [LICENSE](LICENSE) file for details.

## Author

Copyright (c) 2021-2026 Alexander Yunker

## Contributing

Contributions are welcome. What to build with, which rules break the build and how a change is proposed are described in [CONTRIBUTING.md](CONTRIBUTING.md). What has changed from release to release is in [CHANGELOG.md](CHANGELOG.md).

## Support

If you have a question or a problem, open an [issue](https://github.com/AlexanderYunker1983/GCodeGenerator/issues/new/choose) — in English or in Russian, whichever suits you.
Attach the application log to a bug report — `%LOCALAPPDATA%\GCodeGenerator\logs\gcodegenerator.log`: it keeps the text of the exception, which is otherwise lost together with the closed message box.

To report a vulnerability, use a [private report](https://github.com/AlexanderYunker1983/GCodeGenerator/security/advisories/new) instead of an issue — see [SECURITY.md](SECURITY.md).

# Building 0.2.1

Requires Windows PowerShell, a local installation of Probably Stolen (tested against 0.46D), and a C# 9 compatible Roslyn compiler. The game and its assemblies are not distributed in this repository.

Obtain `csc.exe` from Microsoft.Net.Compilers.Toolset 4.12.0 and install Python with Pillow for artifact checks. Supply your own paths, or set `PROBABLY_STOLEN_GAME_ROOT` and `CSC_PATH`:

```powershell
$env:PROBABLY_STOLEN_GAME_ROOT = 'D:\SteamLibrary\steamapps\common\Probably Stolen'
$env:CSC_PATH = 'D:\Tools\Microsoft.Net.Compilers.Toolset\tasks\net472\csc.exe'
./verify.ps1
./build.ps1
python -m pip install Pillow
python ./verify/validate_artifact.py --game-root $env:PROBABLY_STOLEN_GAME_ROOT
```

Explicit `-GameRoot` and `-Compiler` parameters also work. The build uses deterministic compilation, references the local game's Managed directory, and outputs `package/ScavengerTeams`. It packages only this Mod's assembly, manifest, README, MIT license, four textures, eight language tables, and two generated Chinese locale aliases. Nothing is installed automatically.

The host harness currently has 68 regression checks, including all eight languages and sequential dialogue completion. It uses API doubles and does not run Unity. Artifact checks compare keys, format placeholders, dialogue boundaries, tags, encodings, final textures and packaged bytes. They do not assess native-language fluency or verify rendered UI.

An alternative SDK project is provided:

```powershell
dotnet build ./ScavengerTeams.csproj -c Release -p:GameRoot='D:\SteamLibrary\steamapps\common\Probably Stolen'
```

The SDK path has not been tested on the author's runtime-only machine. It builds a DLL rather than a complete distribution; use the contents packaged by `build.ps1` for a release.

Stop the game before installing. Back up an existing `Mods/ScavengerTeams`, then copy the complete package directory to the game's Mods folder. Enable it through the game and restart when prompted. Do not edit saves, original assemblies, or enable/trust records.

For a source-only release, use `git archive`. Keep `outputs`, `package`, compiler dependencies, Remis runtime state, translation receipts containing local paths, and the private test-money repository outside the public source archive. See [release preparation](OPEN_SOURCE.md).

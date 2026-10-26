# Building LuaTools for Windows

LuaTools is a WPF desktop application targeting .NET 8 on Windows. The official CI entrypoint in this fork is .github/workflows/build.yml.

## GitHub Actions (portable .exe)

1. Merge PR #1 after tests and manual review. Before merging, the PR's own Actions run can already build an EXE.
2. Open GitHub Actions > Build Windows EXE. Choose Run workflow if you want a manual build on main; pushes affecting source/tests also build automatically.
3. Open a successful run, locate Artifacts, and download LuaTools-win-x64-portable.
4. Extract the GitHub artifact archive, then extract LuaTools-win-x64-portable.zip.
5. Launch LuaTools.exe on Windows 10/11 x64. Keep any companion files in the extracted folder.

The build is win-x64, self-contained and untrimmed. You do **not** need a separate .NET Desktop Runtime. The artifact is a ZIP, not an installer or signed Velopack release. Windows SmartScreen may display a warning for unsigned personal builds. Do not bypass a warning for binaries you do not trust.

SHA256SUMS.txt lists hashes for the executable and nested portable ZIP. Compare with PowerShell:

~~~powershell
Get-FileHash .\LuaTools.exe -Algorithm SHA256
~~~

## Build locally on Windows

Install .NET 8 SDK, open PowerShell at the repository root, and run:

~~~powershell
dotnet restore LuaToolsGui.sln
dotnet test LuaToolsGui.sln --configuration Release --no-restore
dotnet publish src/LuaToolsGui/LuaToolsGui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o publish/win-x64
~~~

The app is at publish/win-x64/LuaTools.exe.

## Limitations and quality gates

- GitHub Actions validates compilation and unit tests; it does not run the WPF UI.
- Check DEVELOPMENT-ROADMAP.md for Windows manual smoke tests and staged feature work.
- Some optional application features may download external components at runtime.
- Keep original MIT license and upstream attribution. This is an unsigned personal build.

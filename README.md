# Micro VS

A tiny C# IDE: create a project, edit `.cs` files with syntax highlighting, then build and run them.
Originally written in 2019 with WinForms on .NET Framework, now also ported to .NET 10 so it runs on macOS, Linux and Windows.

The repository holds both versions:

| Folder | Version | Runs on |
|---|---|---|
| [`MicroVS.Avalonia/`](MicroVS.Avalonia) | .NET 10 + [Avalonia](https://avaloniaui.net/) UI + [Roslyn](https://github.com/dotnet/roslyn) compiler | macOS, Linux, Windows |
| [`Micro VS/`](Micro%20VS) | Original: .NET Framework 4.6.1 + WinForms + CodeDOM compiler | Windows |

Both use the same Model-View-Presenter design. The port reuses the original `Model` and `Presenter`, rewrites the `View` in Avalonia, and swaps CodeDOM for Roslyn.

## Run the .NET 10 version

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (on macOS: `brew install dotnet`).

```bash
cd MicroVS.Avalonia
dotnet run
```

- **File > Create Project** makes a folder with a `<name>.micro_sln` project file and a Hello World `Program.cs`.
- **Run** (F5) saves all files, compiles the project to `bin/<name>.dll`, and runs it in a new terminal window.
- **Build** (Shift+Cmd/Ctrl+B) compiles without running. Errors and warnings appear in the bottom panel; double-click one to jump to it.

## Run the original version

Open `Micro VS.sln` in Visual Studio on Windows, restore NuGet packages, and press F5.

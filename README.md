# Lightweight PDF Viewer

*Wanted to test out C# and I needed a portable PDF Viewer, so here you go.*

A small native Windows PDF viewer. As lightweight as it gets.

- Open button + drag and drop
- Ctrl+O shortcut
- Fast embedded rendering with WebView2

## Run

```powershell
dotnet run -- "C:\path\to\file.pdf"
```

## Portable build

```powershell
dotnet publish -c Release -r win-x64
```

Published app is available [here](dont forget to insert the damn link)

## If restore fails (NU1100)

This project includes `NuGet.config` with `nuget.org`. If your machine blocks public NuGet, allow access to:

`https://api.nuget.org/v3/index.json`

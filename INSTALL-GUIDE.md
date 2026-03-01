# Gakungu Water — How to Build the Installer

## What you get
`installer\GakunguWater_Setup_v1.0.exe` — a standard Windows installer users
can double-click to install the app on any Windows 10/11 PC **without needing
.NET installed separately** (the runtime is bundled inside).

---

## One-time prerequisites (on the developer/build PC only)

| Tool | Where to get it | Required? |
|------|----------------|-----------|
| .NET 9 SDK | https://dot.net | ✅ Yes |
| Inno Setup 6 | https://jrsoftware.org/isdl.php | ✅ Yes (free) |

Users who receive the installer need **nothing** pre-installed.

---

## Build steps

### Option A — Automatic (recommended)
Open **PowerShell** in the `GAKUNGU WATER` folder and run:
```powershell
.\build-installer.ps1
```
The script will:
1. Clean any previous publish
2. Build a self-contained `GakunguWater.exe` (no .NET needed on target PC)
3. Run Inno Setup to produce `installer\GakunguWater_Setup_v1.0.exe`

### Option B — Manual steps
```powershell
# 1. Publish the app
dotnet publish GakunguWater\GakunguWater.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none --output publish

# 2. Compile the installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
```

---

## What the installer does on the user's PC

1. Copies `GakunguWater.exe` to `C:\Program Files\GakunguWater\`
2. Creates a **Start Menu** shortcut
3. Offers an optional **Desktop** shortcut
4. Adds an entry to **Settings → Apps** (so it can be uninstalled cleanly)
5. Optionally launches the app immediately after install

The database is stored at:
```
C:\Users\<user>\AppData\Local\GakunguWater\gakungu.db
```
This means data survives an **uninstall + reinstall** (it is NOT deleted on uninstall).

---

## To update the version number
Edit `setup.iss`, change line 10:
```ini
#define AppVersion   "1.1"
```
Then rebuild.

---

## Folder structure after build
```
GAKUNGU WATER\
├── build-installer.ps1   ← Run this to build
├── setup.iss             ← Inno Setup config
├── publish\              ← Self-contained exe (created by build script)
│   └── GakunguWater.exe
└── installer\            ← Final installer (created by build script)
    └── GakunguWater_Setup_v1.0.exe  ← Share this file
```

[Setup]
AppId={{9F8AEE68-7517-4B27-90DB-3351C90D5DB9}}
AppName=Gakungu Water Management System
AppVersion=1.0.0
AppPublisher=Gakungu Community Water Project
DefaultDirName={autopf}\GakunguWater
DisableProgramGroupPage=yes
UsedUserAreasWarning=no
PrivilegesRequired=admin
OutputDir=C:\Users\ELITEBOOK 810\Desktop\GAKUNGU WATER\Installer
OutputBaseFilename=GakunguWater_Setup
SetupIconFile=C:\Users\ELITEBOOK 810\Desktop\GAKUNGU WATER\GakunguWater\Assets\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "C:\Users\ELITEBOOK 810\Desktop\GAKUNGU WATER\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Gakungu Water Management System"; Filename: "{app}\GakunguWater.exe"; IconFilename: "{app}\GakunguWater.exe"
Name: "{autodesktop}\Gakungu Water Management System"; Filename: "{app}\GakunguWater.exe"; Tasks: desktopicon; IconFilename: "{app}\GakunguWater.exe"

[Run]
Filename: "{app}\GakunguWater.exe"; Description: "{cm:LaunchProgram,Gakungu Water Management System}"; Flags: nowait postinstall skipifsilent

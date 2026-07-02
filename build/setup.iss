#ifndef AppVersion
#define AppVersion "1.0.0"
#endif

[Setup]
AppName=Bexio Order Importer
AppVersion={#AppVersion}
AppPublisher=Nils Thomann
DefaultDirName={localappdata}\Programs\BexioOrderImport
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
OutputBaseFilename=BexioOrderImportSetup
SetupIconFile=..\src\BexioOrderImport.Wpf\Resources\app_icon.ico
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\BexioOrderImport.Wpf.exe
WizardStyle=modern

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\Bexio Order Importer"; Filename: "{app}\BexioOrderImport.Wpf.exe"
Name: "{userdesktop}\Bexio Order Importer"; Filename: "{app}\BexioOrderImport.Wpf.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BexioOrderImport.Wpf.exe"; Description: "{cm:LaunchProgram,Bexio Order Importer}"; Flags: nowait postinstall skipifsilent

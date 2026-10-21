#define AppName "Transcriber"
#define AppVersion "1.0.0"
#define AppPublisher "Transcriber"
#define AppExeName "Transcriber.exe"

[Setup]
AppId={{A18E682A-03BB-4C64-B5E7-84D83E201A56}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
OutputDir=release
OutputBaseFilename=TranscriberSetup-x64
SetupIconFile=..\icons\icons.ico
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
VersionInfoVersion=1.0.0.0
VersionInfoProductName={#AppName}
VersionInfoDescription=Установщик приложения для транскрибации аудио и видео
LicenseFile=payload\licenses\THIRD-PARTY-NOTICES.txt

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные ярлыки:"; Flags: unchecked

[Files]
Source: "payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "runtimes\cuda\linux-x64\*,runtimes\cuda12\linux-x64\*,runtimes\win-arm64\*,runtimes\win-x86\*"

[Icons]
Name: "{group}\Transcriber"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\Transcriber"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Запустить Transcriber"; Flags: nowait postinstall skipifsilent

; ImageShot – installationsprogram (Inno Setup 6)
; Bygges i CI:  iscc /DAppVersion=2.0.0 /DSourceDir=..\windows\src\ImageShot\bin\Release\net48 ImageShot.iss
; Installerer pr. bruger (ingen administratorrettigheder) i %LOCALAPPDATA%\Programs\ImageShot.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\windows\src\ImageShot\bin\Release\net48"
#endif

[Setup]
AppId={{6B0E8C1E-3A54-4E3B-9C55-1F6FEB1857C1}
AppName=ImageShot
AppVersion={#AppVersion}
AppVerName=ImageShot {#AppVersion}
AppPublisher=ImageShot
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\ImageShot
DefaultGroupName=ImageShot
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\artifacts\installer
OutputBaseFilename=ImageShot-Setup-{#AppVersion}
SetupIconFile=..\windows\src\ImageShot\app.ico
UninstallDisplayIcon={app}\ImageShot.exe
UninstallDisplayName=ImageShot
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0
ArchitecturesInstallIn64BitMode=x64compatible
; Lukker en kørende ImageShot (programmet bruger denne mutex), så filerne kan erstattes.
AppMutex=Local\ImageShot.Running
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "da"; MessagesFile: "compiler:Languages\Danish.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
en.StartWithWindows=Start ImageShot automatically with Windows
da.StartWithWindows=Start ImageShot automatisk sammen med Windows
de.StartWithWindows=ImageShot automatisch mit Windows starten
es.StartWithWindows=Iniciar ImageShot automáticamente con Windows
en.LaunchNow=Start ImageShot now
da.LaunchNow=Start ImageShot nu
de.LaunchNow=ImageShot jetzt starten
es.LaunchNow=Iniciar ImageShot ahora
en.NeedsNet=ImageShot needs .NET Framework 4.8, which is part of Windows 10 (1903 or newer) and Windows 11. Please run Windows Update and try again.
da.NeedsNet=ImageShot kræver .NET Framework 4.8, som følger med Windows 10 (1903 eller nyere) og Windows 11. Kør Windows Update, og prøv igen.
de.NeedsNet=ImageShot benötigt .NET Framework 4.8, das in Windows 10 (ab 1903) und Windows 11 enthalten ist. Bitte Windows Update ausführen und erneut versuchen.
es.NeedsNet=ImageShot necesita .NET Framework 4.8, incluido en Windows 10 (1903 o posterior) y Windows 11. Ejecuta Windows Update e inténtalo de nuevo.

[Tasks]
Name: "autostart"; Description: "{cm:StartWithWindows}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\ImageShot.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\ImageShot.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\ImageShot"; Filename: "{app}\ImageShot.exe"
Name: "{autodesktop}\ImageShot"; Filename: "{app}\ImageShot.exe"; Tasks: desktopicon

[Registry]
; Samme værdi som programmets egen "Start automatisk sammen med Windows".
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ImageShot"; ValueData: """{app}\ImageShot.exe"""; Tasks: autostart; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ImageShot"; Flags: uninsdeletevalue dontcreatekey
; Programmet læser valget ved første start (ellers slår det selv autostart til).
Root: HKCU; Subkey: "Software\ImageShot"; ValueType: string; ValueName: "InstallerAutostart"; ValueData: "1"; Tasks: autostart; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\ImageShot"; ValueType: string; ValueName: "InstallerAutostart"; ValueData: "0"; Tasks: not autostart; Flags: uninsdeletekey

[Run]
Filename: "{app}\ImageShot.exe"; Description: "{cm:LaunchNow}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c taskkill /im ImageShot.exe /f"; Flags: runhidden; RunOnceId: "StopImageShot"

[Code]
// .NET Framework 4.8 = Release >= 528040
function InitializeSetup(): Boolean;
var
  release: Cardinal;
begin
  Result := True;
  if not RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', release) or (release < 528040) then
  begin
    MsgBox(CustomMessage('NeedsNet'), mbError, MB_OK);
    Result := False;
  end;
end;

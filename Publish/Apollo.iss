#define MyAppName "Apollo Studio"
#define MyAppVersion "1.9.0"
#define MyAppPublisher "mat1jaczyyy"
#define MyAppURL "apollo.mat1jaczyyy.com"
#define MyAppExeName "Apollo.exe"

[Setup]
AppId={{BE7DB952-7C93-4DF7-B256-3C14F64088CF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=auto
DisableProgramGroupPage=yes
UsedUserAreasWarning=no
LicenseFile=..\LICENSE
OutputDir=..\Dist\
OutputBaseFilename=Apollo-{#MyAppVersion}-Win
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=yes
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "clearpreferences"; Description: "Clear Preferences"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "novationusbmidi.exe"; DestDir: {tmp}; Flags: deleteafterinstall
Source: "..\Build\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Registry]
Root: HKLM; Subkey: "Software\Classes\.approj"; ValueType: string; ValueName: ""; ValueData: "ApolloStudioProject"; Flags: uninsdeletevalue 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioProject"; ValueType: string; ValueName: ""; ValueData: "Apollo Studio Project"; Flags: uninsdeletekey 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioProject\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Apollo\{#MyAppExeName},0" 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioProject\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Apollo\{#MyAppExeName}"" ""%1""" 
Root: HKLM; Subkey: "Software\Classes\.aptrk"; ValueType: string; ValueName: ""; ValueData: "ApolloStudioTrack"; Flags: uninsdeletevalue 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioTrack"; ValueType: string; ValueName: ""; ValueData: "Apollo Studio Track"; Flags: uninsdeletekey 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioTrack\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Apollo\{#MyAppExeName},0" 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioTrack\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Apollo\{#MyAppExeName}"" ""%1""" 
Root: HKLM; Subkey: "Software\Classes\.apchn"; ValueType: string; ValueName: ""; ValueData: "ApolloStudioChain"; Flags: uninsdeletevalue 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioChain"; ValueType: string; ValueName: ""; ValueData: "Apollo Studio Chain"; Flags: uninsdeletekey 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioChain\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Apollo\{#MyAppExeName},0" 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioChain\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Apollo\{#MyAppExeName}"" ""%1""" 
Root: HKLM; Subkey: "Software\Classes\.apdev"; ValueType: string; ValueName: ""; ValueData: "ApolloStudioDevice"; Flags: uninsdeletevalue 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioDevice"; ValueType: string; ValueName: ""; ValueData: "Apollo Studio Device"; Flags: uninsdeletekey 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioDevice\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Apollo\{#MyAppExeName},0" 
Root: HKLM; Subkey: "Software\Classes\ApolloStudioDevice\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Apollo\{#MyAppExeName}"" ""%1""" 

[InstallDelete]
Type: files; Name: "{%USERPROFILE}\.apollostudio\Apollo.config"; Tasks: clearpreferences

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\Apollo\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Apollo\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\novationusbmidi.exe"; StatusMsg: Installing Novation USB Driver...
Filename: "{app}\Apollo\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
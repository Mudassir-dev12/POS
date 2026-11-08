; ShopPOS Inno Setup Script
; High-performance, offline desktop POS application for 64-bit Windows

#define MyAppName "ShopPOS"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ShopPOS"
#define MyAppURL "https://github.com/ShopPOS/ShopPOS"
#define MyAppExeName "ShopPOS.UI.exe"

[Setup]
; Unique AppId for upgrade detection
AppId={{D37B4C91-6458-456F-9C1D-89EA807D6293}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={commonpf64}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=ShopPOS-Setup-v{#MyAppVersion}
SetupIconFile=..\ShopPOS.UI\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
AppMutex=Global\ShopPOS_SingleInstance_Mutex
CloseApplications=yes
CloseApplicationsFilter=ShopPOS.UI.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startwithwindows"; Description: "Start ShopPOS automatically when Windows boots"; GroupDescription: "Windows Startup:"; Flags: unchecked

[Files]
; Main published application binaries and assets
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\ShopPOS.UI\app.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\googledrive.client.sample.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\prerequisites\README.md"; DestDir: "{app}\prerequisites"; Flags: ignoreversion

; Bundled offline prerequisites: Microsoft Visual C++ 2015-2022 Redistributable (x64)
Source: "..\prerequisites\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ShopPOS"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startwithwindows

[Run]
; Silent installation of Visual C++ Redistributable ONLY if missing from target PC
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; Check: NeedsVCRedist; StatusMsg: "Installing Microsoft Visual C++ 2015-2022 Redistributable (x64)..."; Flags: runhidden
; Launch application option
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
var
  ShouldDeleteData: Boolean;

function NeedsVCRedist(): Boolean;
var
  Installed: Cardinal;
begin
  Result := True;
  // Check 64-bit registry key
  if RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) and (Installed = 1) then
  begin
    Result := False;
    Exit;
  end;
  // Check 32-bit registry view
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) and (Installed = 1) then
  begin
    Result := False;
    Exit;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  // Close any running instance before upgrading
  Exec('taskkill.exe', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
begin
  Result := True;
  GetWindowsVersionEx(Version);

  // Require Windows 10 Version 1809 (Build 17763) or newer / Windows 11
  if (Version.Major < 10) or ((Version.Major = 10) and (Version.Build < 17763)) then
  begin
    SuppressibleMsgBox(
      'ShopPOS requires 64-bit Windows 10 Version 1809 (Build 17763) or Windows 11.' + #13#10#13#10 +
      'Installation cannot continue on this version of Windows.',
      mbCriticalError, MB_OK, IDOK);
    Result := False;
    Exit;
  end;
end;

function GetUserDataFolder(): String;
var
  LocalAppDir: String;
  AppSettingsPath: String;
  Lines: TArrayOfString;
  I: Integer;
  Line: String;
  KeyPos: Integer;
begin
  Result := 'C:\ShopData';

  LocalAppDir := ExpandConstant('{localappdata}\ShopPOS');
  AppSettingsPath := LocalAppDir + '\appsettings.user.json';

  if FileExists(AppSettingsPath) and LoadStringsFromFile(AppSettingsPath, Lines) then
  begin
    for I := 0 to GetArrayLength(Lines) - 1 do
    begin
      Line := Lines[I];
      KeyPos := Pos('"DataDirectory"', Line);
      if KeyPos > 0 then
      begin
        Delete(Line, 1, KeyPos + Length('"DataDirectory"'));
        KeyPos := Pos('"', Line);
        if KeyPos > 0 then
        begin
          Delete(Line, 1, KeyPos);
          KeyPos := Pos('"', Line);
          if KeyPos > 0 then
          begin
            Result := Copy(Line, 1, KeyPos - 1);
            StringChange(Result, '\\', '\');
            Exit;
          end;
        end;
      end;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
var
  MsgRes: Integer;
begin
  Result := True;
  ShouldDeleteData := False;

  if UninstallSilent then
  begin
    // In silent mode, NEVER delete data unless explicit parameter passed
    if Pos('/DELETEDATA=1', UpperCase(GetCmdTail)) > 0 then
      ShouldDeleteData := True;
    Exit;
  end;

  // Prompt the user about data retention (Default is NO to preserve database & backups)
  MsgRes := SuppressibleMsgBox(
    'Do you want to permanently delete your shop database and backup files?' + #13#10#13#10 +
    'Select NO to keep your database and backups safely on disk (Recommended).' + #13#10 +
    'Select YES only if you wish to permanently destroy all shop data.',
    mbConfirmation, MB_YESNO, IDNO);

  if MsgRes = IDYES then
  begin
    ShouldDeleteData := True;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if ShouldDeleteData then
    begin
      DataDir := GetUserDataFolder();
      if (DataDir <> '') and DirExists(DataDir) then
      begin
        DelTree(DataDir, True, True, True);
      end;
      // Also clean up %LocalAppData%\ShopPOS
      DelTree(ExpandConstant('{localappdata}\ShopPOS'), True, True, True);
    end;
  end;
end;

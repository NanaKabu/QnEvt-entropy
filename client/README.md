# QnEvt Windows Client

This is the open-source Windows telemetry client for QnEvt-compatible physical entropy nodes.

The client is published so users can inspect how local entropy telemetry is read, checked, and uploaded. It does not contain production server secrets.

## Features

- Connects to serial entropy hardware.
- Supports automatic port discovery.
- Performs local duplicate and stuck-stream checks before upload.
- Streams entropy telemetry to a configured QnEvt endpoint with a push key or node token.
- Saves local configuration between runs.
- Protects the saved push key with Windows current-user data protection when running on Windows.
- Cleans old diagnostic logs by week.

## Build

Requirements:

- Windows
- .NET SDK compatible with the target framework in `Qnevt.csproj`

Build:

```powershell
dotnet build .\Qnevt.csproj -c Release
```

Publish a self-contained Windows x64 single-file executable:

```powershell
dotnet publish .\Qnevt.csproj -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  /p:DebugType=None `
  /p:DebugSymbols=false
```

## Local Configuration

The client stores local settings under the current Windows user's application data directory:

```text
%APPDATA%\QnEvt\client-settings.json
```

The push key is encrypted with current-user Windows data protection on Windows. A copied settings file should not be expected to decrypt under a different Windows user account.

## Logs

Diagnostic logs are written under the application's local `logs` directory. Files are named:

```text
QnEvt_yyyyMMdd.log
```

The client keeps the current week's logs and deletes older matching QnEvt log files.

## Security Notes

- Do not paste real push keys or node tokens into issues, screenshots, or logs.
- Use one credential per node or bridge host when possible.
- Revoke a push credential immediately if a host is retired or suspected to be compromised.
- Treat the physical entropy device and the workstation running this client as part of the trusted telemetry boundary.

## License

Licensed under the GNU Affero General Public License v3.0. See [../LICENSE](../LICENSE).

# MulticastProxy MSI installer

This folder contains a WiX v5 project that packages the published `MulticastProxy.Service` output plus the `MulticastProxy.DebugViewer` desktop tool as an MSI.

## Prerequisites

- .NET SDK 10+
- WiX Toolset build support via NuGet restore (`WixToolset.Sdk`)

## Build flow

1. Publish the service:

```powershell
dotnet publish .\src\MulticastProxy.Service\MulticastProxy.Service.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\artifacts\publish\win-x64
```

2. Build the MSI:

```powershell
dotnet build .\installer\MulticastProxy.Installer.wixproj -c Release
```

By default the MSI `ProductVersion` is auto-generated from the current UTC build timestamp, so each normal build gets a newer installer version without manual edits.

You can still stamp an explicit MSI version for a release build:

```powershell
dotnet build .\installer\MulticastProxy.Installer.wixproj -c Release -p:ProductVersion=1.0.1
```

By default the project suppresses ICE validation so CLI builds work in environments where the Windows Installer service is unavailable. If you want a full validation pass on a packaging workstation, build with:

```powershell
dotnet build .\installer\MulticastProxy.Installer.wixproj -c Release -p:SuppressValidation=false
```

The MSI output is produced under `installer\bin\Release`.

## Notes

- Service name: `MulticastProxy`
- Install path: `Program Files\MulticastProxy`
- The WiX project expects publish output under `artifacts\publish\win-x64`.
- Re-running the MSI with the same `ProductVersion` now performs an in-place upgrade, which is useful for test deployments.
- `appsettings.json` is preserved during upgrades so local operator configuration is not replaced.
- During install, the service is created and started automatically.
- During uninstall, the service is stopped and removed.

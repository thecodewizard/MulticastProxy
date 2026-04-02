# MulticastProxy MSI installer

This folder contains a WiX v5 project that packages the published `MulticastProxy.Service` output as an MSI.

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

The MSI output is produced under `installer\bin\Release`.

## Notes

- Service name: `MulticastProxy`
- Install path: `Program Files\MulticastProxy`
- The WiX project expects publish output under `artifacts\publish\win-x64`.
- During install, the service is created and started automatically.
- During uninstall, the service is stopped and removed.

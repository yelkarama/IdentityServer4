# Run the local IdP as a Windows service (no Docker)

This runs the IdentityServer4 sample natively on a Windows work laptop as a
background Windows service, so OSCAR can talk to it over OIDC without Docker.

The service is installed with the portable [Servy CLI](https://github.com/akramelassas/servy)
(`servy-cli.exe`) and listens on **`http://+:5555`** (HTTP only — no certificate).

## Prerequisites on the Windows laptop

- Windows + **PowerShell 5.1** (Windows PowerShell; not PowerShell 7 required).
- **Administrator** rights — installing a Windows service needs elevation (it
  registers in the Service Control Manager). Just run the installer; a UAC prompt
  appears — accept it with your dedicated admin account. The installer
  self-elevates, so no manual "Run as administrator" is needed.
- **Nothing else.** The IdP is published **self-contained (win-x64)**, so the
  .NET runtime is bundled in the zip — no Hosting Bundle, no SDK, no install on
  the target.
- (Only if you zip *source* instead of the published output — see Option B) the
  **.NET 11 SDK**, needed to publish on the Windows laptop.

## 1. Prepare the dropzone

Put both zips in `\\S10001\grpreg$\dropzone\Younes\idp`:

1. `servy-10.8-net48-portable.zip` (Servy, already there).
2. The IdP zip — one of these two options:

   **Option A — publish locally (recommended, no SDK needed on Windows):**

   ```powershell
   # on your Mac / build machine
   cd samples/Quickstarts/2_InteractiveAspNetCore/src/IdentityServer
   dotnet publish -c Release -r win-x64 --self-contained true -o "$env:TEMP\idp-build\idp"

   # Zip under a top-level 'idp/' folder so it extracts to C:\Devtools\idp
   Compress-Archive -Path "$env:TEMP\idp-build\idp" -DestinationPath '\\S10001\grpreg$\dropzone\Younes\idp\idp-app.zip' -Force
   ```

   Zip the output under a top-level `idp/` folder (as above) and drop it in the
   dropzone. The installer extracts it into `C:\Devtools`, so the self-contained
   `IdentityServer.exe` lands in `C:\Devtools\idp` and runs directly. The zip is
   ~120 MB (it bundles the .NET runtime).

   **Option B — zip the source tree:**

   Zip from the repo root (or anywhere that keeps
   `samples/Quickstarts/Directory.Build.targets` in the path above
   `IdentityServer.csproj`). That CPM file provides the version for the versionless
   `Serilog.AspNetCore`; without it, `dotnet publish` fails with `NU1015`.
   The installer will publish it (needs the .NET 11 SDK on the Windows laptop).

## 2. Install and start the service

If PowerShell blocks the script ("running scripts is disabled on this system"),
check the effective policies and, if the block isn't from group policy, loosen
it for your user:

```powershell
Get-ExecutionPolicy -List
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

The script is Authenticode-signed, so it also satisfies a stricter `AllSigned`
policy. If the blocking policy comes from group policy (`MachinePolicy` or
`UserPolicy` rows in the list above), a per-user setting can't override it —
use a one-shot bypass instead:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-IdPService.ps1
```

Open **PowerShell as Administrator** and run:

```powershell
cd samples/Quickstarts/2_InteractiveAspNetCore
.\Install-IdPService.ps1
```

The script:

- extracts Servy,
- publishes (or reuses) the IdP in `C:\Devtools\idp`,
- installs the **`idp`** service — automatic start, `http://+:5555`,
  `ASPNETCORE_ENVIRONMENT=Development`,
- starts it and prints the discovery-endpoint status.

## 3. Verify

```powershell
Get-Service idp                 # Status = Running
curl -s http://localhost:5555/.well-known/openid-configuration
# -> 200 + OIDC discovery metadata
```

The signing key is written to `C:\Devtools\idp\tempkey.jwk` and **persists across
restarts** (Servy pins the service's working directory with `--startupDir`). Do not
delete that file — a new key would invalidate already-issued tokens.

## Manage the service

```powershell
$servy = 'C:\Devtools\servy\servy-cli.exe'

& $servy start   -n idp -q     # start
& $servy stop    -n idp -q     # stop
& $servy remove  -n idp -q     # remove (same as .\Install-IdPService.ps1 -Uninstall)
```

You can also start/stop it from the **Windows Services** console (`services.msc`,
service name `idp`, display name `OSCAR IdentityServer`).

## Notes

- HTTP only. The app derives its issuer/Authority from the incoming request host, so
  OSCAR should point at `http://<this-laptop>:5555`.
- `Program.cs` calls `UseWindowsService()`, which is a no-op unless the app runs under
  a Windows service — so the same build still runs fine with `dotnet run` or in Docker.

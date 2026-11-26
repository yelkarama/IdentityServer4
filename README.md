# IdentityServer4 — maintained .NET 11 fork

A maintained fork of [IdentityServer4](https://github.com/DuendeArchive/IdentityServer4) (base: **4.1.2**, Apache License 2.0).

The upstream project is archived — new development moved to the commercial Duende.IdentityServer. This fork keeps the open-source IS4 alive on modern .NET: it targets **.NET 11**, patches the known open-redirect vulnerability, and updates the IdentityModel stack to current versions. It also includes an [OSCAR EMR](#running-the-oscar-quickstart) ready-to-run quickstart and Docker setup.

## What's different from 4.1.2

| Area | Change |
|---|---|
| Security | **CVE-2024-39694** (open redirect) fixed in `src/IdentityServer4/src/Extensions/StringsExtensions.cs` (`IsLocalUrl`) |
| Target framework | `net11.0` for all 4 `IdentityServer4.sln` projects (core, host, unit tests, integration tests); `global.json` pins SDK 11.0.100 |
| Clock API | `ISystemClock` replaced by `TimeProvider` (`UtcNow` → `GetUtcNow()`) across the codebase |
| Crypto APIs | `X509CertificateLoader` for certificate/PKCS12 loading, `RandomNumberGenerator.Create()` |
| IdentityModel | `Microsoft.IdentityModel.*` / `System.IdentityModel.Tokens.Jwt` 8.19.2, Newtonsoft.Json 13.0.3 (pinned in `src/IdentityServer4/Directory.Build.targets`) |
| SI.T.Jwt 8.x fixes | JSON-valued claims converted to plain .NET objects in `TokenExtensions`; `JsonElement` handling in `JwtRequestValidator` (8.x changed payload value types) |
| Tests | Both suites build and run on net11: **724 unit + 293 integration tests passing**, 0 build warnings |

The public API is unchanged — existing IS4 4.1.2 integrations compile against this fork without code changes.

## Repository layout

| Path | Contents |
|---|---|
| `src/IdentityServer4/` | **The fork** (net11): core library, host sample, unit tests, integration tests |
| `src/Storage/`, `src/EntityFramework*/`, `src/AspNetIdentity/` | Upstream sibling solutions, untouched (still netcoreapp3.1) |
| `samples/Quickstarts/2_InteractiveAspNetCore/` | OSCAR IdP quickstart (IdentityServer + Api) |
| `Dockerfile.identityserver-local`, `docker-compose.yml` | Containerized OSCAR IdP |
| `docs/` | Upstream documentation (API unchanged, still accurate) |

## Build & test

Requires the .NET **11.0.100** SDK (pinned by `global.json`).

```bash
dotnet build src/IdentityServer4/IdentityServer4.sln   # 0 warnings
dotnet test src/IdentityServer4/test/IdentityServer.UnitTests
dotnet test src/IdentityServer4/test/IdentityServer.IntegrationTests
```

## Running the OSCAR quickstart

The quickstart 2 sample is configured as an OSCAR EMR identity provider:

- **Client**: `EMR-client` — public PKCE client, redirect `http://localhost:8080/oscar/oauth2/callback` (plus 8070/18080/… variants), scopes include `oscar_profile` which issues the `accountNt` claim OSCAR requires
- **Test user**: `oscardoc` / `aaaa` (with `accountNt=oscardoc`)

### Local

```bash
cd samples/Quickstarts/2_InteractiveAspNetCore
dotnet run --project src/IdentityServer   # IdP on https://localhost:5001
dotnet run --project src/Api              # API on https://localhost:6001
```

### Docker

```bash
docker compose up --build   # IdP on http://localhost:5555
```

Note: compose uses two **external** volumes, `idp_dp-keys` and `idp_is-keys`, which hold the data-protection and signing keys. They must already exist on the daemon — do not delete or recreate them.

### Smoke check

1. `GET /.well-known/openid-configuration` → 200
2. Authorization-code + PKCE flow with `EMR-client` → code at the callback
3. Token exchange → 200, `id_token` with `typ: JWT` and an `accountNt` claim
4. Api with the access token → 200; with a garbage token → 401

## Relationship to upstream

The `upstream` remote points at `DuendeArchive/IdentityServer4`. Fork-only changes:

```bash
git fetch upstream
git diff upstream/main...HEAD -- src/IdentityServer4/src
```

## Security

See [SECURITY.md](SECURITY.md). CVE-2024-39694 is patched in this fork (upstream declined to fix it).

## License

[Apache License 2.0](LICENSE) — same as upstream.

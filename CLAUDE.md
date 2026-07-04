# CLAUDE.md

Guidance for working in this repo. See `README.md` for the full architecture write-up.

## What this is

`scrat` is a .NET 9 CLI that reads objects from S3 and exports them to SMB or FTP.
`scrat <smb|ftp> <key1> [key2 ...]`. Keys are routed to one of three S3 clusters
(Small / Medium / Large) and transferred with a memory strategy matched to the size tier.
Exit code: `0` all transferred, `1` some failed, `2` some not found (none failed); report to
stdout, logs to stderr.

## Commands

```bash
dotnet build                                   # whole solution
dotnet test                                    # all xUnit projects
dotnet test Scrat.Core.Tests/Scrat.Core.Tests.csproj   # single project
dotnet run --project Scrat -- ftp my/object/key
docker build -t scrat .                        # multi-stage, runtime-only final image
```

Projects live at the repo root (no `src/` folder): `Scrat` (CLI host), `Scrat.Core`,
`Scrat.Exporters.Smb`, `Scrat.Exporters.Ftp`, and matching `*.Tests` projects.

## Layout & naming conventions

- **Interfaces go in a nested `Abstractions` folder/namespace under their feature**, e.g.
  `Scrat.Core.S3.Abstractions.IS3Endpoint`, `Scrat.Core.Exporting.Abstractions.IExporter`,
  `Scrat.Core.Transfer.Abstractions.ITransferStrategy`. Concrete types stay in the feature
  namespace (`Scrat.Core.S3.BucketInfo`, `Scrat.Core.S3.S3EndpointResolver`).
- `using` directives are ordered System-first, then alphabetical.
- Build settings are centralized in `Directory.Build.props` (net9.0, nullable, implicit usings,
  `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true`). Warnings are errors — keep the
  build clean, including XML-doc crefs.

## Central Package Management

NuGet versions are centralized in `Directory.Packages.props` via `<PackageVersion>`. Project
`<PackageReference>` entries must NOT carry a `Version` attribute. To add/bump a package, edit
`Directory.Packages.props`. A `PackageReference` with no matching `PackageVersion` fails the build.

## Architecture (key seams)

- `IScratService` (ScratService) orchestrates: fans keys out with `Parallel.ForEachAsync`
  (bounded by `TransferOptions.MaxConcurrency`), aggregates per-key outcomes.
- `IS3EndpointResolver` (S3EndpointResolver) probes the 3 endpoints in ascending size order and
  returns the first that actually holds the key (`IS3Reader.ObjectExistsAsync`, a HEAD on the
  object — not just the bucket, since `BucketInfo.Small` matches almost any key). Bucket names come
  from `endpoint.BucketInfo.Resolve(key)`.
- `BucketInfo` holds each tier's naming rule (`BucketInfo.Small` / `Medium` / `Large`).
- `ITransferStrategy` per tier — Small/Medium buffer then `IExporter.WriteAsync`; Large streams
  via `IExporter.OpenAsync` → `WriteChunkAsync` (per chunk) → `CloseAsync`.
- `IExporter`: `WriteAsync` (whole payload — exporter decides how to slice) + the
  `Open`/`WriteChunk`/`Close` streaming lifecycle + `AbortStreamAsync`. The exporter owns
  open/close and tracks stream position; strategies do not pass isFirst/isLast flags.
- **Resilience**: every atomic I/O action runs in its own named Polly v8 pipeline (see
  `ResiliencePipelineNames`), applied by decorators (`ResilientS3Reader`, `ResilientExporter`) so
  implementations stay retry-free. `NonRetryableExportException`, cancellation, and invalid-data
  are never retried.

## Testing

- xUnit + NSubstitute. Interfaces are mocked; the exporters use hand-written stateful fakes
  (`FakeSmbWorld`, `FakeFtpWorld`) in each test project's `TestDoubles/` folder because tests
  assert on reconstructed file content and resume behavior a mock can't model.
- Run `dotnet test` after changes; keep all tests green.

## Git workflow

Commit directly to `main` and push — this repo does not use feature branches or PRs.
End commit messages with the `Co-Authored-By: Claude ...` trailer.

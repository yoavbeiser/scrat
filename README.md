# Scrat

CLI tool that reads objects from S3 and exports them to SMB or FTP. Objects are automatically routed to the correct S3 cluster (Small / Medium / Large) and transferred using a memory strategy matched to their size tier.

## Usage

```
scrat <smb|ftp> <key1> [key2 key3 ...]
```

```bash
scrat ftp my/object/key
scrat smb 2024-06-01/report key-a key-b
```

Exit code is `0` if all keys transferred successfully, `1` otherwise. The report is written to stdout; logs go to stderr.

## Output

```
[OK      ] my/object/key
[NOT_FOUND] missing/key
[FAILED  ] bad/key  (connection refused)

2 ok  |  1 not found  |  1 failed
```

## Solution layout

| Project | Contents |
|---------|----------|
| `src/Scrat.Core` | Abstractions, S3 endpoints, deserializers, transfer strategies, orchestration, Polly resilience |
| `src/Scrat.Exporters.Smb` | SMB destination (SMBLibrary), offset-addressed writes with resumable stream sessions |
| `src/Scrat.Exporters.Ftp` | FTP destination (FluentFTP), append-based stream sessions with size-verified resume |
| `src/Scrat` | CLI host: argument parsing, configuration, DI wiring, report output |
| `tests/*` | xUnit + NSubstitute unit tests for all of the above |

## Transfer flow

```
CLI args
  └─ ScratService                        (orchestrates, limits concurrency)
       ├─ S3EndpointComposite            (finds which cluster holds the key)
       │    └─ IS3Endpoint × 3           (Small / Medium / Large — probe each in order)
       │         └─ IS3Reader            (AWS S3 wire calls, each wrapped in its own Polly pipeline)
       ├─ ITransferStrategySelector      (picks strategy by endpoint's SizeCategory)
       │    └─ ITransferStrategy         (moves bytes from endpoint to exporter)
       │         └─ IDataDeserializer    (decodes S3 wire format — not used for Large)
       └─ IExporterFactory → IExporter   (SMB or FTP destination, each write wrapped in its own Polly pipeline)
```

### 1. Finding the endpoint

`S3EndpointComposite` iterates the three endpoints in ascending size order (Small → Medium → Large). For each it derives the bucket name from the key, then calls `HeadBucket` via `endpoint.Reader.BucketExistsAsync`. The first cluster whose bucket exists wins and is returned as an `S3EndpointMatch` (endpoint + bucket name). If none respond, the key is reported as `NOT_FOUND`. Endpoints whose naming convention rejects the key shape are skipped without a network call.

| Cluster | Bucket pattern | Expected key format |
|---------|---------------|---------------------|
| Small | `small-data-{key[0..2].lower}` | any |
| Medium | `medium-data-{key.Split('/')[0]}` | `YYYY-MM-DD/{name}` |
| Large | `large-data-{key.Split('-')[0]}` | `{type}-{id}` |

### 2. Selecting a transfer strategy

`TransferStrategySelector` maps the winning endpoint's `HandledSizeCategory` to one of three strategies. No additional size measurement is needed — the endpoint itself encodes the right strategy.

| Size category | Read | Deserialize | Write |
|---------------|------|-------------|-------|
| Small | `Reader.ReadAllAsync` — full object buffered | JSON: decode base64 `payload`, extract `metadata` | `WriteAsync` — single atomic write |
| Medium | `Reader.ReadChunksAsync` → buffer — full object buffered | Binary: skip 4-byte header prefix, extract content bytes | `WriteChunkedAsync` — sequential slices |
| Large | `Reader.ReadChunksAsync` — one chunk at a time, never fully buffered | none — raw bytes pass through | `WriteStreamChunkAsync` — chunk-by-chunk; `isFirst`/`isLast` signal file open/close |

### 3. Writing to the destination

`IExporter` has three write methods matching the three strategies above:
- `WriteAsync` — write the full deserialized payload in one call
- `WriteChunkedAsync` — write the deserialized payload in sequential slices
- `WriteStreamChunkAsync` — write one raw byte chunk; caller passes `isFirst` (create/open) and `isLast` (flush/close)
- `AbortStreamAsync` — release a stream session after a terminal failure (called by `LargeTransferStrategy` on error)

`SmbExporter` (SMBLibrary) writes at explicit offsets, so retried chunks rewrite the same region. `FtpExporter` (FluentFTP) appends; after a connection fault it resumes only when the remote size still equals the committed offset, otherwise the key fails with a non-retryable error.

## Resilience

Every atomic I/O action runs inside its **own named Polly v8 pipeline** (retry with exponential backoff + jitter, plus a per-attempt timeout):

```
s3.bucket-exists   s3.read-all   s3.get-object-size   s3.read-range
exporter.write     exporter.write-chunked             exporter.write-stream-chunk
```

The pipelines are applied by decorators (`ResilientS3Reader`, `ResilientExporter`) so implementations stay retry-free. Chunked reads are composed from `GetObjectSizeAsync` + `ReadRangeAsync`, which means **each ranged fetch of a large object is retried independently**. Cancellation, invalid data and `NonRetryableExportException` are never retried; AWS 4xx errors are not retried, 5xx/408/429 are. Tune via the `Resilience` config section.

## Interface summary

| Interface | Responsibility |
|-----------|---------------|
| `IScratService` | Public entry point. Accepts `TransferRequest`, fans keys out concurrently, returns `TransferResult`. |
| `IS3EndpointComposite` | Probes endpoints in order to find which cluster holds the key. Returns `S3EndpointMatch` or `null`. |
| `IS3Endpoint` | Endpoint metadata: `HandledSizeCategory`, `Deserializer`, `ResolveBucketName`, and `Reader`. |
| `IS3Reader` | Atomic S3 wire operations: `BucketExists`, `ReadAll`, `GetObjectSize`, `ReadRange`; `ReadChunks` is composed from the last two. One instance per endpoint. |
| `IS3ReaderFactory` | Constructs `IS3Reader` from `S3EndpointConfig`. Injection point for swapping the AWS SDK. |
| `IDataDeserializer` | Converts raw S3 bytes into `ExportData`. One implementation per endpoint format. |
| `ITransferStrategySelector` | Maps `SizeCategory` → `ITransferStrategy`. |
| `ITransferStrategy` | Moves data for one key: reads from `IS3Reader`, optionally deserializes, writes to exporter. |
| `IExporterFactory` | Resolves `ExporterType` to the matching `IExporter` singleton. |
| `IExporter` | Writes `ExportData` or raw byte chunks to the destination (SMB or FTP). |

## Configuration

`src/Scrat/appsettings.json`; every value can be overridden with environment variables using `__` as the section separator (e.g. `S3Endpoints__Small__AccessKey`, `Smb__Password`, `Ftp__Host`) — use this for secrets in deployment.

```json
{
  "S3Endpoints": {
    "Small":  { "ServiceUrl": "...", "AccessKey": "...", "SecretKey": "...", "Region": "us-east-1" },
    "Medium": { "ServiceUrl": "...", "AccessKey": "...", "SecretKey": "...", "Region": "us-east-1" },
    "Large":  { "ServiceUrl": "...", "AccessKey": "...", "SecretKey": "...", "Region": "us-east-1" }
  },
  "TransferOptions": {
    "MediumReadChunkSizeBytes":  5242880,
    "MediumWriteChunkSizeBytes": 4194304,
    "LargeChunkSizeBytes":       8388608,
    "MaxConcurrency":            4
  },
  "Resilience": {
    "MaxRetryAttempts": 3,
    "BaseDelayMs": 200,
    "MaxDelayMs": 5000,
    "AttemptTimeoutSeconds": 100,
    "UseJitter": true
  },
  "Smb": { "SharePath": "\\\\server\\share", "Username": "", "Password": "", "Domain": "" },
  "Ftp": { "Host": "ftp.host", "Port": 21, "Username": "", "Password": "", "BasePath": "/" }
}
```

`ServiceUrl` targets S3-compatible clusters (path-style addressing); leave it empty and set `Region` to use AWS directly.

## Build, test, run

Requires the .NET 9.0 SDK.

```bash
dotnet build
dotnet test
dotnet run --project src/Scrat -- ftp my/object/key
dotnet publish src/Scrat/Scrat.csproj -c Release -o out
```

## Docker

```bash
docker build -t scrat .
docker run --rm \
  -e S3Endpoints__Small__ServiceUrl=... \
  -e Smb__Password=... \
  scrat smb 2024-06-01/report
```

The image is multi-stage (SDK build → runtime-only final image) and runs as a non-root user.

## Extending

**Add a new S3 tier:** implement `S3EndpointBase` → add an `ITransferStrategy` → register both in `AddScratCore`.

**Add a new exporter:** implement `IExporter` in a new project → add an `ExporterType` value → register with `services.AddResilientExporter<FooExporter>()`; the factory discovers it automatically.

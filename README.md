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

Exit code is `0` if all keys transferred successfully, `1` otherwise.

## Output

```
[OK      ] my/object/key
[NOT_FOUND] missing/key
[FAILED  ] bad/key  (connection refused)

2 ok  |  1 not found  |  1 failed
```

## Transfer flow

```
CLI args
  └─ ScratService                        (orchestrates, limits concurrency)
       ├─ S3EndpointComposite            (finds which cluster holds the key)
       │    └─ IS3Endpoint × 3           (Small / Medium / Large — probe each in order)
       │         └─ IS3Reader            (AWS S3 wire calls)
       ├─ ITransferStrategySelector      (picks strategy by endpoint's SizeCategory)
       │    └─ ITransferStrategy         (moves bytes from endpoint to exporter)
       │         └─ IDataDeserializer    (decodes S3 wire format — not used for Large)
       └─ IExporterFactory → IExporter   (SMB or FTP destination)
```

### 1. Finding the endpoint

`S3EndpointComposite` iterates the three endpoints in ascending size order (Small → Medium → Large). For each it derives the bucket name from the key, then calls `HeadBucket` via `endpoint.Reader.BucketExistsAsync`. The first cluster whose bucket exists wins and is returned as an `S3EndpointMatch` (endpoint + bucket name). If none respond, the key is reported as `NOT_FOUND`.

Bucket names are derived from the key according to the cluster's naming convention:

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
| Medium | `Reader.ReadChunksAsync` → `MemoryStream` — full object buffered | Binary: skip 4-byte header prefix, extract content bytes | `WriteChunkedAsync` — sequential slices |
| Large | `Reader.ReadChunksAsync` — one chunk at a time, never fully buffered | none — raw bytes pass through | `WriteStreamChunkAsync` — chunk-by-chunk; `isFirst`/`isLast` signal file open/close |

### 3. Writing to the destination

`IExporter` has three write methods matching the three strategies above:
- `WriteAsync` — write the full deserialized payload in one call
- `WriteChunkedAsync` — write the deserialized payload in sequential slices
- `WriteStreamChunkAsync` — write one raw byte chunk; caller passes `isFirst` (create/open) and `isLast` (flush/close)

Both `SmbExporter` and `FtpExporter` are stubs — see source comments for production-ready replacements (SMBLibrary, FluentFTP).

## Interface summary

| Interface | Responsibility |
|-----------|---------------|
| `IScratService` | Public entry point. Accepts `TransferRequest`, fans keys out concurrently, returns `TransferResult`. |
| `IS3EndpointComposite` | Probes endpoints in order to find which cluster holds the key. Returns `S3EndpointMatch` or `null`. |
| `IS3Endpoint` | Endpoint metadata: `HandledSizeCategory`, `Deserializer`, `ResolveBucketName`, and `Reader`. |
| `IS3Reader` | Low-level S3 wire operations: BucketExists, ReadAll, ReadChunks. One instance per endpoint. |
| `IS3ReaderFactory` | Constructs `IS3Reader` from `S3EndpointConfig`. Injection point for swapping the AWS SDK. |
| `IDataDeserializer` | Converts raw S3 bytes into `ExportData`. One implementation per endpoint format. |
| `ITransferStrategySelector` | Maps `SizeCategory` → `ITransferStrategy`. |
| `ITransferStrategy` | Moves data for one key: reads from `IS3Reader`, optionally deserializes, writes to exporter. |
| `IExporterFactory` | Resolves `ExporterType` to the matching `IExporter` singleton (via injected dictionary). |
| `IExporter` | Writes `ExportData` or raw byte chunks to the destination (SMB or FTP). |

## Configuration

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
  "Smb": { "SharePath": "\\\\server\\share", "Username": "", "Password": "", "Domain": "" },
  "Ftp": { "Host": "ftp.host", "Port": 21, "Username": "", "Password": "", "BasePath": "/" }
}
```

## Build

```bash
dotnet build Scrat/Scrat.csproj
dotnet publish Scrat/Scrat.csproj -c Release
```

Requires .NET 9.0 SDK.

## Extending

**Add a new S3 tier:** implement `S3EndpointBase` → add a strategy → register both in `Program.cs`.

**Add a new exporter:** implement `IExporter` → add `ExporterType.Foo` → add to the dictionary in `Program.cs`.

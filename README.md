# PdfAForge

A lightweight ASP.NET WebAPI service that converts standard PDF files to **PDF/A-3B** compliant archives using [iText 9](https://itextpdf.com/).

Built for enterprise environments requiring long-term archiving compliance (French e-invoicing standard NF Z 55-300 / Factur-X, ISO 19005-3).

---

## Features

- PDF to PDF/A-3B conversion via HTTP
- **Concurrency queue** — simultaneous requests wait for a free slot; no requests dropped under burst load
- **ICC profile cached at startup** — loaded once into memory, never read from disk during conversions
- **Content-Length pre-check** — oversized requests rejected before body is buffered
- **Live conversion metrics** — total requests, successes, failures, busy count and average duration exposed on the health endpoint
- Pass-through for already PDF/A-3 compliant files (XMP metadata detection)
- Automatic pre-conversion fixes:
  - Annotation F flags (Print=1, Invisible/Hidden/NoView/ToggleNoView=0)
  - Appearance dictionary cleanup (strip D and R keys)
  - AA/A action entries stripped from annotations, pages and form fields
  - Forbidden annotation types removed (3D, Screen, Movie, Sound, FileAttachment, Watermark)
  - Image fixes: Alternates, OPI keys removed; Interpolate forced to false
  - Form XObject fixes: OPI, PS, Subtype2 keys removed
  - AFRelationship key added/fixed on file spec dictionaries
  - Page AA (Additional Actions) stripped
- Input validation (PDF magic bytes, file size limit)
- Rotating daily log files with configurable retention
- Enriched health check endpoint
- Request correlation ID tracing (X-Correlation-Id header)
- Zero hardcoded values — fully driven by `Web.config`

### Known limitations

The following violations cannot be auto-fixed and will result in a conversion error:

- Encrypted PDFs (password-protected)
- PDFs using forbidden filters (LZWDecode)
- PDFs with forbidden stream keys (FFilter, FDecodeParams)
- DeviceCMYK color space without matching OutputIntent
- Invalid rendering intent values (located in ExtGState or content streams)
- Widget annotations with missing or invalid appearance dictionaries
- Structurally corrupt PDFs (malformed numeric data, invalid PdfName/PdfString length)

---

## Requirements

- Windows Server 2012+ / IIS 8+
- .NET Framework 4.8
- sRGB ICC profile (`sRGB_CS_profile.icm`) — [download from color.org](https://www.color.org/srgbprofiles.xalter)

---

## Endpoints

### `POST /api/convert/pdfa3b`

Converts a PDF to PDF/A-3B.

**Request**
```
Content-Type: multipart/form-data
Body: pdf_file (binary)
Headers: X-Correlation-Id (optional — generated if absent)
```

**Response — success**
```
HTTP 200
Content-Type: application/pdf
Headers:
  X-Correlation-Id
  X-Input-Size-Kb
  X-Output-Size-Kb
  X-Duration-Ms
Body: PDF/A-3B binary
```

**Response — error**
```
HTTP 400  Missing or invalid pdf_file part
HTTP 413  File exceeds MaxFileSizeMb
HTTP 415  File is not a valid PDF (magic bytes check)
HTTP 500  Conversion failed (message included in JSON body)
HTTP 503  All conversion slots busy and QueueTimeoutSeconds elapsed
          Headers: Retry-After: 10
```

---

### `GET /api/convert/health`

Returns service health status.

**Response**
```json
{
  "status": "ok",
  "service": "PdfAForge",
  "version": "1.0.0",
  "timestamp": "2026-04-10 09:00:00",
  "iccProfileOk": true,
  "logPathOk": true,
  "logDiskFreeMb": 45000,
  "maxFileSizeMb": 50,
  "logRetentionDays": 30,
  "maxConcurrentConversions": 4,
  "queueTimeoutSeconds": 120,
  "conversionSlotsAvailable": 3,
  "totalRequests": 142,
  "totalSuccesses": 139,
  "totalFailures": 2,
  "totalBusy": 1,
  "averageDurationMs": 312.4,
  "uptimeSince": "2026-04-20 08:00:00"
}
```

`status` can be `ok` or `degraded` (if ICC profile or log path is unavailable).  
`conversionSlotsAvailable` reflects live semaphore state — useful for monitoring.

---

## Configuration (`Web.config`)

```xml
<appSettings>
  <add key="LogPath"                   value="C:\Logs\PdfAForge\" />
  <add key="LogRetentionDays"          value="30" />
  <add key="MaxFileSizeMb"             value="50" />
  <add key="IccProfilePath"            value="Resources\sRGB_CS_profile.icm" />
  <add key="MaxConcurrentConversions"  value="4" />   <!-- optional, default: CPU count, range 1–64 -->
  <add key="QueueTimeoutSeconds"       value="120" /> <!-- optional, default: 120, range 10–600 -->
  <add key="ServiceVersion"            value="1.0.0" />
  <add key="ServiceName"               value="PdfAForge" />
</appSettings>
```

| Key | Required | Default | Description |
|---|---|---|---|
| `LogPath` | Yes | — | Directory for daily rotating log files |
| `LogRetentionDays` | Yes | — | Days to keep log files (1–365) |
| `MaxFileSizeMb` | Yes | — | Max accepted PDF size (1–500) |
| `IccProfilePath` | Yes | — | Path to sRGB ICC profile (absolute or relative to app root) |
| `MaxConcurrentConversions` | No | CPU count | Max simultaneous iText conversions (1–64) |
| `QueueTimeoutSeconds` | No | 120 | Seconds a queued request waits before receiving 503 (10–600) |
| `ServiceVersion` | No | `1.0.0` | Reported in health endpoint |
| `ServiceName` | No | `PdfAForge` | Reported in health endpoint and PDF metadata |

---

## Project Structure

```
PdfAForge/
├── App_Start/
│   └── WebApiConfig.cs
├── Config/
│   └── AppSettings.cs          — typed Web.config access + startup validation
├── Controllers/
│   └── ConvertController.cs    — HTTP endpoints
├── Logging/
│   └── ConversionLogger.cs     — rotating daily log + retention cleanup
├── Models/
│   ├── ConversionResult.cs
│   └── HealthStatus.cs
├── Resources/
│   └── sRGB_CS_profile.icm     — mandatory ICC color profile
├── Services/
│   ├── PdfConverterService.cs  — PDF/A-3B conversion engine
│   └── ConversionMetrics.cs    — in-memory request counters (thread-safe)
├── Validation/
│   └── PdfValidator.cs         — magic bytes + file size validation
├── Global.asax                 — startup validation + logging
└── Web.config
```

---

## Getting Started

1. Clone the repo
2. Place `sRGB_CS_profile.icm` in `Resources/` → set **Copy to Output Directory = Copy always**
3. Install NuGet packages:
```
Install-Package itext -Version 9.x
Install-Package itext.bouncy-castle-adapter
Install-Package Microsoft.AspNet.WebApi -Version 5.2.9
Install-Package Newtonsoft.Json -Version 13.0.3
```
4. Update `Web.config` with your log path
5. Build and deploy to IIS

---

## License

MIT — Copyright (c) 2026 [AlexArtaud-Dev](https://github.com/AlexArtaud-Dev)
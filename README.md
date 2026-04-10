# PdfAForge

A lightweight ASP.NET WebAPI service that converts standard PDF files to **PDF/A-3B** compliant archives using [iText 9](https://itextpdf.com/).

Built for enterprise environments requiring long-term archiving compliance (French e-invoicing standard NF Z 55-300 / Factur-X, ISO 19005-3).

---

## Features

- PDF to PDF/A-3B conversion via HTTP
- Automatic annotation compliance fixing
- Input validation (magic bytes, file size)
- Rotating daily log files with configurable retention
- Enriched health check endpoint
- Request correlation ID tracing
- Zero hardcoded values — fully driven by `Web.config`

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
Headers: X-Correlation-Id (optional)
```

**Response**
```
Content-Type: application/pdf
Headers:
  X-Correlation-Id
  X-Input-Size-Kb
  X-Output-Size-Kb
  X-Duration-Ms
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
  "logRetentionDays": 30
}
```

---

## Configuration (`Web.config`)

```xml
<appSettings>
  <add key="LogPath"          value="C:\Logs\PdfAForge\" />
  <add key="LogRetentionDays" value="30" />
  <add key="MaxFileSizeMb"    value="50" />
  <add key="IccProfilePath"   value="Resources\sRGB_CS_profile.icm" />
  <add key="ServiceVersion"   value="1.0.0" />
  <add key="ServiceName"      value="PdfAForge" />
</appSettings>
```

---

## Project Structure

```
PdfAForge/
├── App_Start/
│   └── WebApiConfig.cs
├── Config/
│   └── AppSettings.cs
├── Controllers/
│   └── ConvertController.cs
├── Logging/
│   └── ConversionLogger.cs
├── Models/
│   ├── ConversionResult.cs
│   └── HealthStatus.cs
├── Resources/
│   └── sRGB_CS_profile.icm
├── Services/
│   └── PdfConverterService.cs
├── Validation/
│   └── PdfValidator.cs
├── Global.asax
└── Web.config
```

---

## Getting Started

1. Clone the repo
2. Place `sRGB_CS_profile.icm` in `Resources/` and set **Copy to Output Directory = Copy always**
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
# BarcodeScanner Plugin

A multi-format barcode and QR code scanning plugin for .NET and web applications. Built with an ML-assisted preprocessing pipeline for accurate scanning of blurry, damaged, or low-contrast images.

## Features

- Scans **16+ barcode formats**: QR Code, Code 128, Code 39, EAN-13, EAN-8, UPC-A, UPC-E, Data Matrix, Aztec, PDF417, ITF, Codabar, RSS-14, RSS Expanded, Code 93, Codabar
- **3-stage pipeline**: OpenCV preprocessing → YOLO ML detection → ZXing.Net decoding
- **Preprocessing**: denoising, CLAHE contrast enhancement, sharpening, auto-deskew, binarization
- **ONNX ML detection**: plug in any YOLOv8 `.onnx` model to locate barcode regions in complex images
- **REST API**: use from any language or framework via HTTP
- **JavaScript SDK**: file upload, base64, and live camera scanning

---

## Project Structure

```
barcode-scanner/
├── BarcodeScanner.Core/        # C# library — use directly in any .NET app
│   ├── BarcodeScannerEngine.cs
│   ├── Config/ScannerOptions.cs
│   ├── Models/ScanResult.cs
│   └── Pipeline/
│       ├── PreprocessingStage.cs
│       ├── DetectionStage.cs
│       └── DecodingStage.cs
├── BarcodeScanner.Api/         # ASP.NET Core REST API
│   ├── Controllers/ScanController.cs
│   └── appsettings.json
├── BarcodeScanner.Js/          # JavaScript SDK
│   └── src/scanner.js
└── demo/
    └── index.html              # Live browser demo
```

---

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Windows runtime (OpenCvSharp4.runtime.win is included)
- *(Optional)* Python + `ultralytics` to convert YOLO model to ONNX

---

## Getting Started

### 1. Clone and build

```bash
git clone <your-repo-url>
cd barcode-scanner
dotnet build
```

### 2. Run the API

```bash
cd BarcodeScanner.Api
dotnet run
```

API is available at `http://localhost:5000`.  
Swagger UI is at `http://localhost:5000/swagger`.

### 3. (Optional) Add a YOLO model for ML detection

Download a pre-trained barcode detection model:

**Option A — Barcode-specific (recommended)**  
1. Download from [Piero2411/YOLOV8s-Barcode-Detection](https://huggingface.co/Piero2411/YOLOV8s-Barcode-Detection)
2. Convert to ONNX (requires Python):
   ```bash
   pip install ultralytics
   python -c "from ultralytics import YOLO; YOLO('YOLOV8s_Barcode_Detection.pt').export(format='onnx')"
   ```

**Option B — Pre-built ONNX (no Python needed)**  
Direct download from [AXERA-TECH/YOLOv8](https://huggingface.co/AXERA-TECH/YOLOv8/resolve/main/yolov8s.onnx) (44.9 MB, MIT license).

Then set the path in `BarcodeScanner.Api/appsettings.json`:

```json
"ScannerOptions": {
  "EnableMlDetection": true,
  "ModelPath": "C:/models/YOLOV8s_Barcode_Detection.onnx"
}
```

---

## API Reference

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/scan/upload` | Scan from uploaded image file (multipart/form-data) |
| `POST` | `/api/scan/base64` | Scan from base64-encoded image (JSON body) |
| `GET`  | `/api/scan/health` | Health check |

### POST /api/scan/upload

```http
POST /api/scan/upload
Content-Type: multipart/form-data

file: <image file>
```

### POST /api/scan/base64

```http
POST /api/scan/base64
Content-Type: application/json

{
  "image": "data:image/png;base64,iVBORw0KGgo..."
}
```

### Response format

```json
{
  "success": true,
  "processingTimeMs": 143,
  "barcodes": [
    {
      "value": "https://example.com",
      "format": "QR_CODE",
      "confidence": 1.0,
      "boundingBox": { "x": 42, "y": 18, "width": 200, "height": 200 }
    }
  ],
  "errorMessage": null
}
```

---

## Usage in C#

### A. Use the Core library directly (no API needed)

Add a reference to `BarcodeScanner.Core`:

```xml
<ProjectReference Include="..\BarcodeScanner.Core\BarcodeScanner.Core.csproj" />
```

#### Scan from a file

```csharp
using BarcodeScanner.Core;
using BarcodeScanner.Core.Config;

var engine = new BarcodeScannerEngine();
var result = engine.ScanFromFile("path/to/image.png");

if (result.Success)
{
    foreach (var barcode in result.Barcodes)
        Console.WriteLine($"[{barcode.Format}] {barcode.Value}");
}
```

#### Scan from a byte array

```csharp
byte[] imageBytes = File.ReadAllBytes("barcode.jpg");
var result = engine.ScanFromBytes(imageBytes);
```

#### Scan from a stream

```csharp
using var stream = File.OpenRead("barcode.png");
var result = engine.ScanFromStream(stream);
```

#### Custom options

```csharp
var engine = new BarcodeScannerEngine(new ScannerOptions
{
    EnablePreprocessing = true,
    EnableMlDetection = true,
    ModelPath = @"C:\models\YOLOV8s_Barcode_Detection.onnx",
    MaxResults = 5,
    Preprocessing = new PreprocessingOptions
    {
        Denoise = true,
        EnhanceContrast = true,
        Sharpen = true,
        AutoRotate = true,
        Binarize = true
    }
});
```

### B. Use the API from C# (HttpClient)

#### Scan from a file

```csharp
using var client = new HttpClient();
using var form = new MultipartFormDataContent();
using var fileStream = File.OpenRead("barcode.png");

form.Add(new StreamContent(fileStream), "file", "barcode.png");

var response = await client.PostAsync("http://localhost:5000/api/scan/upload", form);
var json = await response.Content.ReadAsStringAsync();
Console.WriteLine(json);
```

#### Scan from base64

```csharp
using var client = new HttpClient();

byte[] bytes = File.ReadAllBytes("barcode.png");
string base64 = Convert.ToBase64String(bytes);

var payload = new StringContent(
    $"{{\"image\":\"{base64}\"}}",
    System.Text.Encoding.UTF8,
    "application/json"
);

var response = await client.PostAsync("http://localhost:5000/api/scan/base64", payload);
var json = await response.Content.ReadAsStringAsync();
Console.WriteLine(json);
```

#### Use with ASP.NET Core dependency injection

```csharp
// Program.cs
builder.Services.AddSingleton(new ScannerOptions { EnablePreprocessing = true });
builder.Services.AddSingleton<BarcodeScannerEngine>();

// YourController.cs
public class YourController : ControllerBase
{
    private readonly BarcodeScannerEngine _scanner;

    public YourController(BarcodeScannerEngine scanner)
    {
        _scanner = scanner;
    }

    [HttpPost("scan")]
    public IActionResult Scan(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var result = _scanner.ScanFromStream(stream);
        return Ok(result);
    }
}
```

---

## Usage in JavaScript

Include the SDK in your HTML:

```html
<script src="BarcodeScanner.Js/src/scanner.js"></script>
```

Or import it as a module:

```js
import BarcodeScannerPlugin from './BarcodeScanner.Js/src/scanner.js';
```

### Scan from a file input

```html
<input type="file" id="file-input" accept="image/*" />

<script>
const scanner = new BarcodeScannerPlugin({
  apiUrl: 'http://localhost:5000'
});

document.getElementById('file-input').addEventListener('change', async (e) => {
  const file = e.target.files[0];
  const result = await scanner.scanFile(file);

  if (result.success) {
    result.barcodes.forEach(b => {
      console.log(`${b.format}: ${b.value}`);
    });
  }
});
</script>
```

### Scan from a base64 string

```js
const scanner = new BarcodeScannerPlugin({ apiUrl: 'http://localhost:5000' });

// Works with full data URL or raw base64
const result = await scanner.scanBase64('data:image/png;base64,iVBORw0KGgo...');
console.log(result.barcodes);
```

### Scan from an image element

```js
const img = document.getElementById('my-image');
const result = await scanner.scanImage(img);
```

### Live camera scanning

```html
<video id="camera" autoplay muted playsinline></video>
<canvas id="canvas" style="display:none"></canvas>

<button onclick="startScan()">Start</button>
<button onclick="stopScan()">Stop</button>

<script>
const scanner = new BarcodeScannerPlugin({
  apiUrl: 'http://localhost:5000',
  videoElementId: 'camera',
  canvasElementId: 'canvas',
  scanInterval: 500,           // scan every 500ms
  onResult: (result) => {
    console.log('Found:', result.barcodes[0].value);
  },
  onError: (err) => {
    console.error(err);
  }
});

async function startScan() { await scanner.startCamera('environment'); }
function stopScan()         { scanner.stopCamera(); }
</script>
```

### Check API health

```js
const status = await BarcodeScannerPlugin.checkHealth('http://localhost:5000');
console.log(status); // { status: 'ok', version: '1.0.0' }
```

---

## Usage in Angular

### 1. Copy the SDK or install it

Copy `BarcodeScanner.Js/src/scanner.js` into your Angular project at `src/assets/scanner.js`,
or add it to `angular.json`:

```json
"scripts": ["src/assets/scanner.js"]
```

### 2. Create a service

```bash
ng generate service barcode-scanner
```

```typescript
// barcode-scanner.service.ts
import { Injectable } from '@angular/core';

declare const BarcodeScannerPlugin: any;

export interface ScanResult {
  success: boolean;
  processingTimeMs: number;
  errorMessage: string | null;
  barcodes: Array<{
    value: string;
    format: string;
    confidence: number;
    boundingBox: { x: number; y: number; width: number; height: number } | null;
  }>;
}

@Injectable({ providedIn: 'root' })
export class BarcodeScannerService {
  private scanner: any;

  constructor() {
    this.scanner = new BarcodeScannerPlugin({
      apiUrl: 'http://localhost:5000'
    });
  }

  scanFile(file: File): Promise<ScanResult> {
    return this.scanner.scanFile(file);
  }

  scanBase64(base64: string): Promise<ScanResult> {
    return this.scanner.scanBase64(base64);
  }

  async checkHealth(): Promise<{ status: string; version: string }> {
    return BarcodeScannerPlugin.checkHealth('http://localhost:5000');
  }
}
```

### 3. Use in a component

```typescript
// scan.component.ts
import { Component } from '@angular/core';
import { BarcodeScannerService, ScanResult } from './barcode-scanner.service';

@Component({
  selector: 'app-scan',
  template: `
    <input type="file" accept="image/*" (change)="onFile($event)" />

    <div *ngIf="result">
      <p *ngIf="!result.success" style="color:red">
        No barcode found. ({{ result.processingTimeMs }}ms)
      </p>
      <ul *ngIf="result.success">
        <li *ngFor="let b of result.barcodes">
          <strong>{{ b.format }}</strong>: {{ b.value }}
        </li>
      </ul>
    </div>

    <p *ngIf="error" style="color:red">{{ error }}</p>
  `
})
export class ScanComponent {
  result: ScanResult | null = null;
  error: string | null = null;

  constructor(private scannerService: BarcodeScannerService) {}

  async onFile(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.error = null;
    this.result = null;

    try {
      this.result = await this.scannerService.scanFile(file);
    } catch (err: any) {
      this.error = err.message;
    }
  }
}
```

### 4. Live camera in Angular

```typescript
// camera-scan.component.ts
import { Component, ElementRef, ViewChild, OnDestroy } from '@angular/core';

declare const BarcodeScannerPlugin: any;

@Component({
  selector: 'app-camera-scan',
  template: `
    <video #cameraVideo autoplay muted playsinline style="width:100%"></video>
    <canvas #cameraCanvas style="display:none"></canvas>

    <p *ngIf="lastValue">Last scan: <strong>{{ lastValue }}</strong></p>

    <button (click)="start()">Start Camera</button>
    <button (click)="stop()">Stop</button>
  `
})
export class CameraScanComponent implements OnDestroy {
  @ViewChild('cameraVideo') videoRef!: ElementRef<HTMLVideoElement>;
  @ViewChild('cameraCanvas') canvasRef!: ElementRef<HTMLCanvasElement>;

  lastValue = '';
  private scanner: any;

  async start() {
    // IDs must match the element IDs in the template
    this.videoRef.nativeElement.id  = 'ng-camera-video';
    this.canvasRef.nativeElement.id = 'ng-camera-canvas';

    this.scanner = new BarcodeScannerPlugin({
      apiUrl: 'http://localhost:5000',
      videoElementId: 'ng-camera-video',
      canvasElementId: 'ng-camera-canvas',
      scanInterval: 600,
      onResult: (result: any) => {
        if (result.barcodes.length > 0)
          this.lastValue = result.barcodes[0].value;
      }
    });

    await this.scanner.startCamera('environment');
  }

  stop() {
    this.scanner?.stopCamera();
  }

  ngOnDestroy() {
    this.scanner?.stopCamera();
  }
}
```

---

## Configuration reference

All options in `appsettings.json` (API) or `ScannerOptions` (C# library):

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `EnablePreprocessing` | bool | `true` | Run OpenCV preprocessing pipeline |
| `EnableMlDetection` | bool | `false` | Use YOLO model to locate barcode regions |
| `ModelPath` | string | `null` | Path to `.onnx` model file |
| `MaxResults` | int | `10` | Max barcodes to return per image |
| `Preprocessing.Denoise` | bool | `true` | FastNlMeans denoising |
| `Preprocessing.EnhanceContrast` | bool | `true` | CLAHE adaptive contrast |
| `Preprocessing.Sharpen` | bool | `true` | Unsharp mask sharpening |
| `Preprocessing.AutoRotate` | bool | `true` | Hough-line deskew |
| `Preprocessing.Binarize` | bool | `true` | Otsu + adaptive thresholding |

---

## Supported barcode formats

| Format | Example use |
|--------|-------------|
| QR Code | URLs, vCards, payments |
| Data Matrix | Product labels, medical |
| Aztec | Boarding passes |
| PDF417 | ID cards, shipping |
| Code 128 | Logistics, retail |
| Code 39 | Industrial, automotive |
| Code 93 | Inventory |
| EAN-13 | Retail products |
| EAN-8 | Small retail products |
| UPC-A | North American retail |
| UPC-E | Compact retail |
| ITF | Shipping cartons |
| Codabar | Libraries, blood banks |
| RSS-14 / RSS Expanded | GS1 supply chain |

---

## License

MIT

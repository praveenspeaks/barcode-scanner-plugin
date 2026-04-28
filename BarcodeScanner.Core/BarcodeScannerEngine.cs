using System.Diagnostics;
using BarcodeScanner.Core.Config;
using BarcodeScanner.Core.Models;
using BarcodeScanner.Core.Pipeline;
using OpenCvSharp;

namespace BarcodeScanner.Core;

public sealed class BarcodeScannerEngine : IDisposable
{
    private readonly ScannerOptions _options;
    private readonly PreprocessingStage _preprocessor;
    private readonly DetectionStage _detector;
    private readonly DecodingStage _decoder;

    public BarcodeScannerEngine(ScannerOptions? options = null)
    {
        _options = options ?? new ScannerOptions();
        _preprocessor = new PreprocessingStage(_options.Preprocessing);
        _detector = new DetectionStage(_options.ModelPath);
        _decoder = new DecodingStage();
    }

    public ScanResult ScanFromBytes(byte[] imageBytes)
    {
        using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (mat.Empty())
            return Fail("Could not decode image bytes.");
        return Scan(mat);
    }

    public ScanResult ScanFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return Fail($"File not found: {filePath}");
        using var mat = Cv2.ImRead(filePath, ImreadModes.Color);
        if (mat.Empty())
            return Fail("Could not read image file.");
        return Scan(mat);
    }

    public ScanResult ScanFromStream(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ScanFromBytes(ms.ToArray());
    }

    private ScanResult Scan(Mat image)
    {
        var sw = Stopwatch.StartNew();
        var found = new List<BarcodeItem>();
        var seenValues = new HashSet<string>();

        try
        {
            var imagesToTry = _options.EnablePreprocessing
                ? _preprocessor.Process(image)
                : [image];

            var regions = _options.EnableMlDetection
                ? _detector.Detect(image)
                : [new BoundingBox { X = 0, Y = 0, Width = image.Width, Height = image.Height }];

            foreach (var variant in imagesToTry)
            {
                foreach (var region in regions)
                {
                    var items = _decoder.Decode(variant, region);
                    foreach (var item in items)
                    {
                        if (seenValues.Add(item.Value))
                        {
                            found.Add(item);
                            if (found.Count >= _options.MaxResults)
                                goto Done;
                        }
                    }
                }
            }

            Done:
            foreach (var variant in imagesToTry)
                variant.Dispose();
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }

        sw.Stop();
        return new ScanResult
        {
            Success = found.Count > 0,
            Barcodes = found,
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };
    }

    private static ScanResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };

    public void Dispose() => _detector.Dispose();
}

using System.Diagnostics;
using BarcodeScanner.Core.Config;
using BarcodeScanner.Core.Models;
using BarcodeScanner.Core.Pipeline;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
        try
        {
            using var image = Image.Load<Rgb24>(imageBytes);
            return Scan(image);
        }
        catch (Exception ex)
        {
            return Fail("Could not decode image: " + ex.Message);
        }
    }

    public ScanResult ScanFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return Fail($"File not found: {filePath}");
        try
        {
            using var image = Image.Load<Rgb24>(filePath);
            return Scan(image);
        }
        catch (Exception ex)
        {
            return Fail("Could not read image file: " + ex.Message);
        }
    }

    public ScanResult ScanFromStream(Stream stream)
    {
        try
        {
            using var image = Image.Load<Rgb24>(stream);
            return Scan(image);
        }
        catch (Exception ex)
        {
            return Fail("Could not decode image stream: " + ex.Message);
        }
    }

    private ScanResult Scan(Image<Rgb24> image)
    {
        var sw = Stopwatch.StartNew();
        var found = new List<BarcodeItem>();
        var seenValues = new HashSet<string>();

        try
        {
            var variants = _options.EnablePreprocessing
                ? _preprocessor.Process(image)
                : [image.Clone()];

            var regions = _options.EnableMlDetection
                ? _detector.Detect(image)
                : [new BoundingBox { X = 0, Y = 0, Width = image.Width, Height = image.Height }];

            foreach (var variant in variants)
            {
                using (variant)
                {
                    foreach (var region in regions)
                    {
                        foreach (var item in _decoder.Decode(variant, region))
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
            }

            Done:;
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

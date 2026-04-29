using BarcodeScanner.Core.Config;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BarcodeScanner.Core.Pipeline;

public class PreprocessingStage
{
    private readonly PreprocessingOptions _options;

    public PreprocessingStage(PreprocessingOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Returns multiple preprocessed variants of the image for ZXing to try.
    /// Each variant targets a different degradation (blur, low contrast, etc).
    /// </summary>
    public List<Image<Rgb24>> Process(Image<Rgb24> input)
    {
        var variants = new List<Image<Rgb24>> { input.Clone() };

        if (_options.EnhanceContrast)
            variants.Add(input.Clone(ctx => ctx.Grayscale().Contrast(0.6f)));

        if (_options.Sharpen)
            variants.Add(input.Clone(ctx => ctx.Grayscale().GaussianSharpen(1.5f)));

        if (_options.Denoise)
            variants.Add(input.Clone(ctx => ctx.Grayscale().GaussianBlur(1.2f)));

        if (_options.Binarize)
        {
            variants.Add(input.Clone(ctx => ctx
                .Grayscale()
                .BinaryThreshold(0.45f)));

            // Inverted — helps with white-on-dark barcodes
            variants.Add(input.Clone(ctx => ctx
                .Grayscale()
                .BinaryThreshold(0.45f)
                .Invert()));
        }

        return variants;
    }
}

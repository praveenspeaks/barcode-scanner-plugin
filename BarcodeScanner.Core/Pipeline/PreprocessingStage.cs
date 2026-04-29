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
    /// Returns preprocessing variants ordered from best-quality to most-aggressive.
    /// ZXing tries them in order and stops once barcodes are found.
    /// </summary>
    public List<Image<Rgb24>> Process(Image<Rgb24> input)
    {
        var variants = new List<Image<Rgb24>>();

        // 1. Original — always try first; clean images decode best unmodified
        variants.Add(input.Clone());

        // 2. Upscaled — helps when barcodes are small relative to image size
        //    (e.g. a full test sheet where each barcode is only ~150px wide)
        if (_options.EnhanceContrast && (input.Width < 1600 || input.Height < 1600))
        {
            variants.Add(input.Clone(ctx =>
                ctx.Resize(input.Width * 2, input.Height * 2)));
        }

        // 3. Grayscale + contrast boost — good for faded or low-contrast prints
        if (_options.EnhanceContrast)
            variants.Add(input.Clone(ctx => ctx.Grayscale().Contrast(0.5f)));

        // 4. Sharpened — helps with slightly blurry camera captures
        if (_options.Sharpen)
            variants.Add(input.Clone(ctx => ctx.Grayscale().GaussianSharpen(1.5f)));

        // 5. Light binarization — helps with noisy or watermarked backgrounds
        if (_options.Binarize)
            variants.Add(input.Clone(ctx => ctx.Grayscale().BinaryThreshold(0.40f)));

        // 6. Strong binarization — helps with very dark/light images
        if (_options.Binarize)
            variants.Add(input.Clone(ctx => ctx.Grayscale().BinaryThreshold(0.60f)));

        // 7. Inverted — for white-on-dark barcodes (ALSO_INVERTED in ZXing handles most,
        //    but an explicitly inverted image as a variant catches edge cases)
        if (_options.Binarize)
            variants.Add(input.Clone(ctx => ctx.Grayscale().Invert()));

        return variants;
    }
}

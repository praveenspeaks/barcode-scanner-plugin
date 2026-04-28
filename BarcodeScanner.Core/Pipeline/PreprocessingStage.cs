using BarcodeScanner.Core.Config;
using OpenCvSharp;

namespace BarcodeScanner.Core.Pipeline;

public class PreprocessingStage
{
    private readonly PreprocessingOptions _options;

    public PreprocessingStage(PreprocessingOptions options)
    {
        _options = options;
    }

    public Mat[] Process(Mat input)
    {
        var variants = new List<Mat> { input.Clone() };

        using var gray = ToGrayscale(input);

        if (_options.Denoise)
        {
            var denoised = new Mat();
            Cv2.FastNlMeansDenoising(gray, denoised, 10, 7, 21);
            variants.Add(denoised);
        }

        if (_options.EnhanceContrast)
        {
            var clahe = Cv2.CreateCLAHE(clipLimit: 3.0, tileGridSize: new Size(8, 8));
            var enhanced = new Mat();
            clahe.Apply(gray, enhanced);
            variants.Add(enhanced);
        }

        if (_options.Sharpen)
        {
            var sharpened = Sharpen(gray);
            variants.Add(sharpened);
        }

        if (_options.Binarize)
        {
            var otsu = new Mat();
            Cv2.Threshold(gray, otsu, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            variants.Add(otsu);

            var adaptive = new Mat();
            Cv2.AdaptiveThreshold(gray, adaptive, 255,
                AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 11, 2);
            variants.Add(adaptive);
        }

        if (_options.AutoRotate)
        {
            var deskewed = Deskew(gray);
            if (deskewed is not null)
                variants.Add(deskewed);
        }

        return [.. variants];
    }

    private static Mat ToGrayscale(Mat src)
    {
        if (src.Channels() == 1)
            return src.Clone();
        var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    private static Mat Sharpen(Mat src)
    {
        var kernel = Mat.FromArray(new float[,]
        {
            { 0, -1,  0 },
            { -1, 5, -1 },
            { 0, -1,  0 }
        });
        var sharpened = new Mat();
        Cv2.Filter2D(src, sharpened, MatType.CV_8U, kernel);
        return sharpened;
    }

    private static Mat? Deskew(Mat src)
    {
        using var edges = new Mat();
        Cv2.Canny(src, edges, 50, 150);

        var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, threshold: 80,
            minLineLength: 50, maxLineGap: 10);

        if (lines.Length == 0)
            return null;

        double angle = lines
            .Select(l => Math.Atan2(l.P2.Y - l.P1.Y, l.P2.X - l.P1.X))
            .Average() * 180.0 / Math.PI;

        if (Math.Abs(angle) < 0.5)
            return null;

        var center = new Point2f(src.Width / 2f, src.Height / 2f);
        var rotMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);
        var deskewed = new Mat();
        Cv2.WarpAffine(src, deskewed, rotMatrix, src.Size(),
            flags: InterpolationFlags.Linear, borderMode: BorderTypes.Replicate);
        return deskewed;
    }
}

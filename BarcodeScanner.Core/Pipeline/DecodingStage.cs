using System.Runtime.InteropServices;
using BarcodeScanner.Core.Models;
using OpenCvSharp;
using ZXing;
using ZXing.Common;
using ZXing.Multi;

namespace BarcodeScanner.Core.Pipeline;

public class DecodingStage
{
    private static readonly IDictionary<DecodeHintType, object> Hints = new Dictionary<DecodeHintType, object>
    {
        [DecodeHintType.TRY_HARDER] = true,
        [DecodeHintType.ALSO_INVERTED] = true,
        [DecodeHintType.POSSIBLE_FORMATS] = new List<BarcodeFormat>
        {
            BarcodeFormat.QR_CODE,
            BarcodeFormat.DATA_MATRIX,
            BarcodeFormat.AZTEC,
            BarcodeFormat.PDF_417,
            BarcodeFormat.CODE_128,
            BarcodeFormat.CODE_39,
            BarcodeFormat.CODE_93,
            BarcodeFormat.EAN_13,
            BarcodeFormat.EAN_8,
            BarcodeFormat.UPC_A,
            BarcodeFormat.UPC_E,
            BarcodeFormat.ITF,
            BarcodeFormat.CODABAR,
            BarcodeFormat.RSS_14,
            BarcodeFormat.RSS_EXPANDED,
        }
    };

    private readonly MultiFormatReader _formatReader = new();

    public List<BarcodeItem> Decode(Mat image, BoundingBox region)
    {
        using var roi = CropRegion(image, region);

        var results = new List<BarcodeItem>();

        // Try both the original orientation and 90° rotations
        foreach (var variant in GetRotationVariants(roi))
        {
            using (variant)
            {
                var found = DecodeOnce(variant, region);
                foreach (var item in found)
                    if (results.All(r => r.Value != item.Value))
                        results.Add(item);
            }
        }

        return results;
    }

    private List<BarcodeItem> DecodeOnce(Mat mat, BoundingBox region)
    {
        var luminance = MatToLuminanceSource(mat);
        var bitmap = new BinaryBitmap(new HybridBinarizer(luminance));
        var multiReader = new GenericMultipleBarcodeReader(_formatReader);

        Result[]? raw;
        try
        {
            raw = multiReader.decodeMultiple(bitmap, Hints);
        }
        catch
        {
            raw = null;
        }

        if (raw is null || raw.Length == 0)
            return [];

        return raw
            .Where(r => r?.Text is not null)
            .Select(r => new BarcodeItem
            {
                Value = r.Text,
                Format = r.BarcodeFormat.ToString(),
                BoundingBox = ToAbsoluteBoundingBox(r.ResultPoints, region),
                Confidence = 1.0f
            })
            .ToList();
    }

    private static RGBLuminanceSource MatToLuminanceSource(Mat src)
    {
        using var rgb = new Mat();
        if (src.Channels() == 1)
            Cv2.CvtColor(src, rgb, ColorConversionCodes.GRAY2RGB);
        else
            Cv2.CvtColor(src, rgb, ColorConversionCodes.BGR2RGB);

        int byteCount = rgb.Width * rgb.Height * 3;
        byte[] bytes = new byte[byteCount];
        Marshal.Copy(rgb.Data, bytes, 0, byteCount);

        return new RGBLuminanceSource(bytes, rgb.Width, rgb.Height, RGBLuminanceSource.BitmapFormat.RGB24);
    }

    private static IEnumerable<Mat> GetRotationVariants(Mat src)
    {
        yield return src.Clone();

        var cw = new Mat();
        Cv2.Rotate(src, cw, RotateFlags.Rotate90Clockwise);
        yield return cw;

        var ccw = new Mat();
        Cv2.Rotate(src, ccw, RotateFlags.Rotate90Counterclockwise);
        yield return ccw;
    }

    private static Mat CropRegion(Mat src, BoundingBox box)
    {
        int x = Math.Max(0, box.X);
        int y = Math.Max(0, box.Y);
        int w = Math.Min(box.Width, src.Width - x);
        int h = Math.Min(box.Height, src.Height - y);

        if (w <= 0 || h <= 0)
            return src.Clone();

        return src[new Rect(x, y, w, h)].Clone();
    }

    private static BoundingBox? ToAbsoluteBoundingBox(ResultPoint[]? points, BoundingBox region)
    {
        if (points is null || points.Length == 0)
            return null;

        float minX = points.Min(p => p.X) + region.X;
        float minY = points.Min(p => p.Y) + region.Y;
        float maxX = points.Max(p => p.X) + region.X;
        float maxY = points.Max(p => p.Y) + region.Y;

        return new BoundingBox
        {
            X = (int)minX,
            Y = (int)minY,
            Width = (int)(maxX - minX),
            Height = (int)(maxY - minY)
        };
    }
}

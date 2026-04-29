using BarcodeScanner.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ZXing;
using ZXing.Common;
using ZXing.Multi;

namespace BarcodeScanner.Core.Pipeline;

public class DecodingStage
{
    private static readonly IDictionary<DecodeHintType, object> Hints =
        new Dictionary<DecodeHintType, object>
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

    public List<BarcodeItem> Decode(Image<Rgb24> image, BoundingBox region)
    {
        var results = new List<BarcodeItem>();

        foreach (var variant in GetRotationVariants(image, region))
        {
            using (variant)
            {
                foreach (var item in DecodeOnce(variant, region))
                    if (results.All(r => r.Value != item.Value))
                        results.Add(item);
            }
        }

        return results;
    }

    private List<BarcodeItem> DecodeOnce(Image<Rgb24> img, BoundingBox region)
    {
        var luminance = ToLuminanceSource(img);
        var bitmap = new BinaryBitmap(new HybridBinarizer(luminance));
        var multiReader = new GenericMultipleBarcodeReader(_formatReader);

        Result[]? raw;
        try { raw = multiReader.decodeMultiple(bitmap, Hints); }
        catch { raw = null; }

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

    private static RGBLuminanceSource ToLuminanceSource(Image<Rgb24> img)
    {
        var bytes = new byte[img.Width * img.Height * 3];
        int idx = 0;
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    bytes[idx++] = row[x].R;
                    bytes[idx++] = row[x].G;
                    bytes[idx++] = row[x].B;
                }
            }
        });
        return new RGBLuminanceSource(bytes, img.Width, img.Height,
            RGBLuminanceSource.BitmapFormat.RGB24);
    }

    private static IEnumerable<Image<Rgb24>> GetRotationVariants(Image<Rgb24> src, BoundingBox region)
    {
        using var roi = CropRegion(src, region);
        yield return roi.Clone();

        var cw = roi.Clone(ctx => ctx.Rotate(RotateMode.Rotate90));
        yield return cw;

        var ccw = roi.Clone(ctx => ctx.Rotate(RotateMode.Rotate270));
        yield return ccw;
    }

    private static Image<Rgb24> CropRegion(Image<Rgb24> src, BoundingBox box)
    {
        int x = Math.Max(0, box.X);
        int y = Math.Max(0, box.Y);
        int w = Math.Min(box.Width, src.Width - x);
        int h = Math.Min(box.Height, src.Height - y);

        if (w <= 0 || h <= 0)
            return src.Clone();

        return src.Clone(ctx => ctx.Crop(new Rectangle(x, y, w, h)));
    }

    private static BoundingBox? ToAbsoluteBoundingBox(ResultPoint[]? points, BoundingBox region)
    {
        if (points is null || points.Length == 0) return null;

        float minX = points.Min(p => p.X) + region.X;
        float minY = points.Min(p => p.Y) + region.Y;
        float maxX = points.Max(p => p.X) + region.X;
        float maxY = points.Max(p => p.Y) + region.Y;

        return new BoundingBox
        {
            X = (int)minX, Y = (int)minY,
            Width = (int)(maxX - minX), Height = (int)(maxY - minY)
        };
    }
}

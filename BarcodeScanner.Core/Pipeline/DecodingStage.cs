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
    private static readonly List<BarcodeFormat> AllFormats =
    [
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
    ];

    public List<BarcodeItem> Decode(Image<Rgb24> image, BoundingBox region)
    {
        using var roi = CropRegion(image, region);
        var found = new List<BarcodeItem>();
        var seen  = new HashSet<string>();

        // Try GlobalHistogramBinarizer — best for printed documents, uniform lighting
        foreach (var item in DecodeWithBinarizer(roi, useGlobal: true, region))
            if (seen.Add(item.Value)) found.Add(item);

        // Try HybridBinarizer — best for photos with uneven lighting
        foreach (var item in DecodeWithBinarizer(roi, useGlobal: false, region))
            if (seen.Add(item.Value)) found.Add(item);

        return found;
    }

    private static List<BarcodeItem> DecodeWithBinarizer(
        Image<Rgb24> img, bool useGlobal, BoundingBox region)
    {
        var luminance = ToLuminanceSource(img);
        Binarizer binarizer = useGlobal
            ? new GlobalHistogramBinarizer(luminance)
            : new HybridBinarizer(luminance);
        var bitmap = new BinaryBitmap(binarizer);

        // Create fresh reader per call — MultiFormatReader is NOT thread-safe
        // and retains internal state that causes missed detections when reused
        var reader  = new MultiFormatReader();
        var multi   = new GenericMultipleBarcodeReader(reader);

        var hints = new Dictionary<DecodeHintType, object>
        {
            [DecodeHintType.TRY_HARDER]       = true,
            [DecodeHintType.ALSO_INVERTED]     = true,
            [DecodeHintType.POSSIBLE_FORMATS]  = AllFormats,
        };

        Result[]? raw;
        try   { raw = multi.decodeMultiple(bitmap, hints); }
        catch { raw = null; }

        if (raw is null || raw.Length == 0)
            return [];

        return raw
            .Where(r => r?.Text is not null)
            .Select(r => new BarcodeItem
            {
                Value       = r.Text,
                Format      = r.BarcodeFormat.ToString(),
                BoundingBox = ToAbsoluteBoundingBox(r.ResultPoints, region),
                Confidence  = 1.0f
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

    private static Image<Rgb24> CropRegion(Image<Rgb24> src, BoundingBox box)
    {
        int x = Math.Max(0, box.X);
        int y = Math.Max(0, box.Y);
        int w = Math.Min(box.Width,  src.Width  - x);
        int h = Math.Min(box.Height, src.Height - y);
        if (w <= 0 || h <= 0) return src.Clone();
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

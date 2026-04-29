using BarcodeScanner.Core.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BarcodeScanner.Core.Pipeline;

/// <summary>
/// Uses a YOLOv8 ONNX model to locate barcode regions before decoding.
/// Falls back to full-image scan when no model is loaded.
/// </summary>
public sealed class DetectionStage : IDisposable
{
    private readonly InferenceSession? _session;
    private const int ModelInputSize = 640;
    private const float ConfidenceThreshold = 0.4f;
    private const float NmsThreshold = 0.45f;

    public DetectionStage(string? modelPath)
    {
        if (modelPath is not null && File.Exists(modelPath))
            _session = new InferenceSession(modelPath);
    }

    public List<BoundingBox> Detect(Image<Rgb24> image)
    {
        if (_session is null)
            return [new BoundingBox { X = 0, Y = 0, Width = image.Width, Height = image.Height }];

        var (resized, scale) = ResizeWithPadding(image, ModelInputSize);
        using (resized)
        {
            var tensor = ImageToTensor(resized);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", tensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();
            return ParseDetections(output, scale, image.Width, image.Height);
        }
    }

    private static (Image<Rgb24> resized, float scale) ResizeWithPadding(Image<Rgb24> src, int targetSize)
    {
        float scaleX = (float)targetSize / src.Width;
        float scaleY = (float)targetSize / src.Height;
        float scale = Math.Min(scaleX, scaleY);

        int newW = (int)(src.Width * scale);
        int newH = (int)(src.Height * scale);

        // Resize maintaining aspect ratio, then pad to square
        var padded = new Image<Rgb24>(targetSize, targetSize, new Rgb24(0, 0, 0));
        var resized = src.Clone(ctx => ctx.Resize(newW, newH));
        padded.Mutate(ctx => ctx.DrawImage(resized, new Point(0, 0), 1f));
        resized.Dispose();

        return (padded, scale);
    }

    private static DenseTensor<float> ImageToTensor(Image<Rgb24> img)
    {
        var tensor = new DenseTensor<float>([1, 3, img.Height, img.Width]);

        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    tensor[0, 0, y, x] = row[x].R / 255f;
                    tensor[0, 1, y, x] = row[x].G / 255f;
                    tensor[0, 2, y, x] = row[x].B / 255f;
                }
            }
        });

        return tensor;
    }

    private static List<BoundingBox> ParseDetections(
        Tensor<float> output, float scale, int imgW, int imgH)
    {
        var boxes = new List<(float x, float y, float w, float h, float conf)>();

        // YOLOv8 output: [1, 84, 8400] — rows=attributes, cols=candidates
        int numCandidates = output.Dimensions[2];
        int numClasses = output.Dimensions[1] - 4;

        for (int i = 0; i < numCandidates; i++)
        {
            float maxScore = 0f;
            for (int c = 0; c < numClasses; c++)
            {
                float s = output[0, 4 + c, i];
                if (s > maxScore) maxScore = s;
            }
            if (maxScore < ConfidenceThreshold) continue;

            float cx = output[0, 0, i] / scale;
            float cy = output[0, 1, i] / scale;
            float bw = output[0, 2, i] / scale;
            float bh = output[0, 3, i] / scale;
            boxes.Add((cx - bw / 2, cy - bh / 2, bw, bh, maxScore));
        }

        return ApplyNms(boxes, NmsThreshold, imgW, imgH);
    }

    private static List<BoundingBox> ApplyNms(
        List<(float x, float y, float w, float h, float conf)> boxes,
        float iouThreshold, int imgW, int imgH)
    {
        var sorted = boxes.OrderByDescending(b => b.conf).ToList();
        var kept = new List<BoundingBox>();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            sorted.RemoveAt(0);
            kept.Add(new BoundingBox
            {
                X = Math.Max(0, (int)best.x),
                Y = Math.Max(0, (int)best.y),
                Width = Math.Min((int)best.w, imgW),
                Height = Math.Min((int)best.h, imgH)
            });
            sorted.RemoveAll(b => Iou(best, b) > iouThreshold);
        }

        return kept;
    }

    private static float Iou(
        (float x, float y, float w, float h, float conf) a,
        (float x, float y, float w, float h, float conf) b)
    {
        float ix = Math.Max(a.x, b.x);
        float iy = Math.Max(a.y, b.y);
        float iw = Math.Min(a.x + a.w, b.x + b.w) - ix;
        float ih = Math.Min(a.y + a.h, b.y + b.h) - iy;
        if (iw <= 0 || ih <= 0) return 0;
        float intersection = iw * ih;
        float union = a.w * a.h + b.w * b.h - intersection;
        return intersection / union;
    }

    public void Dispose() => _session?.Dispose();
}

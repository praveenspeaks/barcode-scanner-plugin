using BarcodeScanner.Core.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace BarcodeScanner.Core.Pipeline;

/// <summary>
/// Uses a YOLO-based ONNX model to locate barcode regions before decoding.
/// Falls back to full-image scan when no model is loaded.
/// </summary>
public class DetectionStage : IDisposable
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

    public List<BoundingBox> Detect(Mat image)
    {
        if (_session is null)
            return [new BoundingBox { X = 0, Y = 0, Width = image.Width, Height = image.Height }];

        var (resized, scale) = ResizeWithPadding(image, ModelInputSize);
        var tensor = MatToTensor(resized);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", tensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        return ParseDetections(output, scale, image.Width, image.Height);
    }

    private static (Mat resized, float scale) ResizeWithPadding(Mat src, int targetSize)
    {
        float scaleX = (float)targetSize / src.Width;
        float scaleY = (float)targetSize / src.Height;
        float scale = Math.Min(scaleX, scaleY);

        int newW = (int)(src.Width * scale);
        int newH = (int)(src.Height * scale);

        var resized = new Mat();
        Cv2.Resize(src, resized, new Size(newW, newH));

        var padded = new Mat(new Size(targetSize, targetSize), src.Type(), Scalar.Black);
        resized.CopyTo(padded[new Rect(0, 0, newW, newH)]);

        return (padded, scale);
    }

    private static DenseTensor<float> MatToTensor(Mat mat)
    {
        var channels = mat.Channels();
        using var rgb = new Mat();
        Cv2.CvtColor(mat, rgb, channels == 1 ? ColorConversionCodes.GRAY2RGB : ColorConversionCodes.BGR2RGB);

        int h = rgb.Height, w = rgb.Width;
        var tensor = new DenseTensor<float>([1, 3, h, w]);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var pixel = rgb.At<Vec3b>(y, x);
            tensor[0, 0, y, x] = pixel.Item2 / 255f;
            tensor[0, 1, y, x] = pixel.Item1 / 255f;
            tensor[0, 2, y, x] = pixel.Item0 / 255f;
        }

        return tensor;
    }

    private static List<BoundingBox> ParseDetections(
        Tensor<float> output, float scale, int imgW, int imgH)
    {
        var boxes = new List<(float x, float y, float w, float h, float conf)>();

        // YOLOv8 ONNX output shape: [1, 84, 8400]
        // Rows = attributes (0-3: xywh, 4-83: class scores)
        // Cols = candidate detections
        int numCandidates = output.Dimensions[2];
        int numAttribs    = output.Dimensions[1];
        int numClasses    = numAttribs - 4;

        for (int i = 0; i < numCandidates; i++)
        {
            float maxClassScore = 0f;
            for (int c = 0; c < numClasses; c++)
            {
                float s = output[0, 4 + c, i];
                if (s > maxClassScore) maxClassScore = s;
            }

            if (maxClassScore < ConfidenceThreshold) continue;

            float cx = output[0, 0, i] / scale;
            float cy = output[0, 1, i] / scale;
            float bw = output[0, 2, i] / scale;
            float bh = output[0, 3, i] / scale;

            boxes.Add((cx - bw / 2, cy - bh / 2, bw, bh, maxClassScore));
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

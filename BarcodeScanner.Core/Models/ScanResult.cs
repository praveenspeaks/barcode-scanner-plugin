namespace BarcodeScanner.Core.Models;

public class ScanResult
{
    public bool Success { get; set; }
    public List<BarcodeItem> Barcodes { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public long ProcessingTimeMs { get; set; }
}

public class BarcodeItem
{
    public string Value { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public BoundingBox? BoundingBox { get; set; }
    public float Confidence { get; set; }
}

public class BoundingBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

namespace BarcodeScanner.Core.Config;

public class ScannerOptions
{
    public bool EnablePreprocessing { get; set; } = true;
    public bool EnableMlDetection { get; set; } = true;
    public int MaxResults { get; set; } = 50;
    public string? ModelPath { get; set; }
    public PreprocessingOptions Preprocessing { get; set; } = new();
}

public class PreprocessingOptions
{
    public bool Denoise { get; set; } = true;
    public bool EnhanceContrast { get; set; } = true;
    public bool Sharpen { get; set; } = true;
    public bool AutoRotate { get; set; } = true;
    public bool Binarize { get; set; } = true;
}

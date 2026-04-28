using BarcodeScanner.Core;
using BarcodeScanner.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace BarcodeScanner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScanController : ControllerBase
{
    private readonly BarcodeScannerEngine _engine;
    private readonly ILogger<ScanController> _logger;

    public ScanController(BarcodeScannerEngine engine, ILogger<ScanController> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>
    /// Scan barcodes from an uploaded image file.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ScanResult>> ScanUpload(IFormFile file)
    {
        if (file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        var allowed = new[] { "image/jpeg", "image/png", "image/bmp", "image/webp", "image/gif" };
        if (!allowed.Contains(file.ContentType.ToLowerInvariant()))
            return BadRequest(new { error = "Unsupported file type." });

        using var stream = file.OpenReadStream();
        var result = _engine.ScanFromStream(stream);

        _logger.LogInformation("Scanned {File}: found {Count} barcode(s) in {Ms}ms",
            file.FileName, result.Barcodes.Count, result.ProcessingTimeMs);

        return Ok(result);
    }

    /// <summary>
    /// Scan barcodes from a base64-encoded image in JSON body.
    /// </summary>
    [HttpPost("base64")]
    public ActionResult<ScanResult> ScanBase64([FromBody] Base64Request request)
    {
        if (string.IsNullOrWhiteSpace(request.Image))
            return BadRequest(new { error = "Image data is required." });

        string base64 = request.Image.Contains(',')
            ? request.Image[(request.Image.IndexOf(',') + 1)..]
            : request.Image;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch
        {
            return BadRequest(new { error = "Invalid base64 image data." });
        }

        if (bytes.Length > 20 * 1024 * 1024)
            return BadRequest(new { error = "Image exceeds 20MB limit." });

        var result = _engine.ScanFromBytes(bytes);
        return Ok(result);
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", version = "1.0.0" });
}

public record Base64Request(string Image);

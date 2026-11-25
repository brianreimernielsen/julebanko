using QRCoder;

namespace Julebanko.Services;

/// <summary>
/// Compact QR payload for printed plates — encodes the zero-padded series display
/// (e.g. <c>000001</c>) so a USB keyboard-wedge scanner pastes the same digits into Tjek plader.
/// </summary>
public static class BingoSeriesBarcode
{
    /// <summary>PNG bytes for a quiet, scannable QR of the series display string.</summary>
    public static byte[] CreateQrPng(string seriesDisplay, int pixelsPerModule = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesDisplay);
        pixelsPerModule = Math.Clamp(pixelsPerModule, 2, 8);

        using var generator = new QRCodeGenerator();
        // Short numeric payload → version-1 QR; ECC M is a good print/scan balance.
        var data = generator.CreateQrCode(seriesDisplay.Trim(), QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        // Quiet zone keeps phone/USB scanners happy without enlarging the plate chrome.
        return png.GetGraphic(pixelsPerModule, drawQuietZones: true);
    }
}

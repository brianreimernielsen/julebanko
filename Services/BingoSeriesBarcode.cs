using System.Buffers.Binary;
using System.IO.Compression;

namespace Julebanko.Services;

/// <summary>
/// Code128 payload for printed plates — encodes the zero-padded series display
/// (e.g. <c>000001</c>) so a USB keyboard-wedge scanner pastes the same digits into Tjek plader.
/// Pure managed PNG (no Skia/System.Drawing) so IIS/Linux publish stays self-contained.
/// </summary>
public static class BingoSeriesBarcode
{
    // Code128 bar/space patterns (values 0–106). '1' = bar, '0' = space. Stop (106) is 13 modules.
    private static readonly string[] Bars =
    [
        "11011001100","11001101100","11001100110","10010011000","10010001100",
        "10001001100","10011001000","10011000100","10001100100","11001001000",
        "11001000100","11000100100","10110011100","10011011100","10011001110",
        "10111001100","10011101100","10011100110","11001110010","11001011100",
        "11001001110","11011100100","11001110100","11101101110","11101001100",
        "11100101100","11100100110","11101100100","11100110100","11100110010",
        "11011011000","11011000110","11000110110","10100011000","10001011000",
        "10001000110","10110001000","10001101000","10001100010","11010001000",
        "11000101000","11000100010","10110111000","10110001110","10001101110",
        "10111011000","10111000110","10001110110","11101110110","11010001110",
        "11000101110","11011101000","11011100010","11011101110","11101011000",
        "11101000110","11100010110","11101101000","11101100010","11100011010",
        "11101111010","11001000010","11110001010","10100110000","10100001100",
        "10010110000","10010000110","10000101100","10000100110","10110010000",
        "10110000100","10011010000","10011000010","10000110100","10000110010",
        "11000010010","11001010000","11110111010","11000010100","10001111010",
        "10100111100","10010111100","10010011110","10111100100","10011110100",
        "10011110010","11110100100","11110010100","11110010010","11011011110",
        "11011110110","11110110110","10101111000","10100011110","10001011110",
        "10111101000","10111100010","11110101000","11110100010","10111011110",
        "10111101110","11101011110","11110101110","11010000100","11010010000",
        "11010011100","1100011101011"
    ];

    /// <summary>
    /// PNG bytes for a short Code128 of the series display string (no HRI text under bars;
    /// the plate already shows <c>Nr. ######</c>).
    /// </summary>
    public static byte[] CreateCode128Png(
        string seriesDisplay,
        int widthPx = 280,
        int heightPx = 48,
        int quietModules = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesDisplay);
        widthPx = Math.Clamp(widthPx, 80, 800);
        heightPx = Math.Clamp(heightPx, 16, 200);
        quietModules = Math.Clamp(quietModules, 4, 20);

        var modules = BuildModules(seriesDisplay.Trim(), quietModules);
        var moduleWidth = Math.Max(1, widthPx / modules.Length);
        var bmpWidth = moduleWidth * modules.Length;
        var bmpHeight = heightPx;

        // 8-bit grayscale PNG, black bars on white.
        var row = new byte[bmpWidth];
        for (var x = 0; x < modules.Length; x++)
        {
            var v = modules[x] ? (byte)0 : (byte)255;
            var start = x * moduleWidth;
            for (var i = 0; i < moduleWidth; i++)
            {
                row[start + i] = v;
            }
        }

        return EncodeGrayPng(bmpWidth, bmpHeight, row);
    }

    private static bool[] BuildModules(string data, int quietModules)
    {
        // Prefer Code128 Subset C for even-length digit strings (series is always D6).
        var values = new List<int>();
        if (data.Length >= 2 && data.Length % 2 == 0 && data.All(char.IsDigit))
        {
            values.Add(105); // Start C
            for (var i = 0; i < data.Length; i += 2)
            {
                values.Add((data[i] - '0') * 10 + (data[i + 1] - '0'));
            }
        }
        else
        {
            values.Add(104); // Start B
            foreach (var ch in data)
            {
                var code = ch - 32;
                if (code is < 0 or > 95)
                {
                    throw new ArgumentException($"Code128 cannot encode character U+{(int)ch:X4}.", nameof(data));
                }

                values.Add(code);
            }
        }

        var checksum = 0;
        for (var i = 0; i < values.Count; i++)
        {
            var weight = i == 0 ? 1 : i;
            checksum += values[i] * weight;
        }

        values.Add(checksum % 103);
        values.Add(106); // Stop

        var bits = new List<bool>(quietModules + values.Count * 11 + quietModules);
        for (var i = 0; i < quietModules; i++)
        {
            bits.Add(false);
        }

        foreach (var v in values)
        {
            var pattern = Bars[v];
            foreach (var c in pattern)
            {
                bits.Add(c == '1');
            }
        }

        for (var i = 0; i < quietModules; i++)
        {
            bits.Add(false);
        }

        return bits.ToArray();
    }

    private static byte[] EncodeGrayPng(int width, int height, byte[] rowPixels)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0); // None filter
            raw.Write(rowPixels, 0, rowPixels.Length);
        }

        var compressed = ZlibCompress(raw.ToArray());

        using var png = new MemoryStream();
        Span<byte> sig = [137, 80, 78, 71, 13, 10, 26, 10];
        png.Write(sig);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..8], height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 0;  // grayscale
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(png, "IHDR"u8, ihdr);

        WriteChunk(png, "IDAT"u8, compressed);
        WriteChunk(png, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return png.ToArray();
    }

    private static byte[] ZlibCompress(byte[] raw)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78);
        ms.WriteByte(0x01);
        using (var deflate = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw, 0, raw.Length);
        }

        var adler = Adler32(raw);
        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, adler);
        ms.Write(checksum);
        return ms.ToArray();
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        uint a = 1, b = 0;
        foreach (var x in data)
        {
            a = (a + x) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        stream.Write(len);
        stream.Write(type);
        stream.Write(data);

        var crc = Crc32(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var x in type)
        {
            crc = CrcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
        }

        foreach (var x in data)
        {
            crc = CrcTable[(crc ^ x) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}

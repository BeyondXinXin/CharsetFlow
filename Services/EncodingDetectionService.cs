using System.Text;
using CharsetFlow.Models;
using UtfUnknown;

namespace CharsetFlow.Services;

internal sealed record EncodingDetection(
    EncodingOption? Encoding,
    float Confidence,
    string LineEnding,
    bool IsText,
    bool IsEmpty,
    string? Detail = null);

internal sealed class EncodingDetectionService
{
    private const int SampleSize = 100 * 1024;

    public async Task<EncodingDetection> DetectFileAsync(string path, CancellationToken cancellationToken)
    {
        byte[] bytes = await ReadPrefixAsync(path, SampleSize, cancellationToken);
        return Detect(bytes);
    }

    public EncodingDetection Detect(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return new EncodingDetection(null, 1F, "无", true, true, "空文件");
        }

        (EncodingOption? bomEncoding, int bomLength) = DetectBom(bytes);
        if (bomEncoding is not null)
        {
            string lineEnding = DetectLineEnding(bytes.AsSpan(bomLength), bomEncoding);
            return new EncodingDetection(bomEncoding, 1F, lineEnding, true, false, "BOM");
        }

        EncodingOption? unicode = DetectBomlessUnicode(bytes);
        if (unicode is not null && TryDecode(bytes, unicode, out string? unicodeText) && LooksLikeText(unicodeText))
        {
            return new EncodingDetection(unicode, .99F, DetectLineEnding(unicodeText), true, false, "严格 Unicode 校验");
        }

        try
        {
            DetectionResult result = CharsetDetector.DetectFromBytes(bytes);
            DetectionDetail? detected = result.Detected;
            if (detected is not null)
            {
                EncodingOption? option = EncodingCatalog.Resolve(detected.EncodingName, detected.Encoding?.CodePage);
                if (option is not null && TryDecode(bytes, option, out string? text) && LooksLikeText(text))
                {
                    float confidence = Math.Clamp(detected.Confidence, 0F, 1F);
                    bool trusted = confidence >= .20F || option.Id == "utf-8";
                    return new EncodingDetection(
                        option,
                        confidence,
                        DetectLineEnding(text),
                        trusted,
                        false,
                        $"uchardet 复合探测：{detected.EncodingName}");
                }
            }
        }
        catch
        {
            // Unknown is a valid detection outcome; binary heuristics below decide whether to include it.
        }

        bool binary = LooksBinary(bytes);
        return new EncodingDetection(null, 0F, "—", !binary, false, binary ? "疑似二进制" : "低置信度文本");
    }

    public async Task<string> GetPreviewAsync(FileItem item, CancellationToken cancellationToken)
    {
        if (item.Size == 0)
        {
            return "（空文件）";
        }

        if (item.SourceEncoding is null)
        {
            return "无法可靠识别编码。可右键文件并手动指定源编码后再预览。";
        }

        byte[] bytes = await ReadPrefixAsync(item.FullPath, 200 * 1024, cancellationToken);
        ReadOnlySpan<byte> content = RemovePreamble(bytes, item.SourceEncoding);
        string text = item.SourceEncoding.CreateEncoding().GetString(content);
        return item.Size > bytes.Length ? text + Environment.NewLine + Environment.NewLine + "… 仅预览前 200 KB" : text;
    }

    public static ReadOnlySpan<byte> RemovePreamble(ReadOnlySpan<byte> bytes, EncodingOption option)
    {
        byte[] preamble = option.GetPreamble();
        return preamble.Length > 0 && bytes.StartsWith(preamble) ? bytes[preamble.Length..] : bytes;
    }

    private static async Task<byte[]> ReadPrefixAsync(string path, int count, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        int length = (int)Math.Min(stream.Length, count);
        byte[] buffer = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return offset == length ? buffer : buffer[..offset];
    }

    private static (EncodingOption? Encoding, int Length) DetectBom(ReadOnlySpan<byte> bytes)
    {
        (byte[] Bom, string Id)[] signatures =
        [
            ([0x00, 0x00, 0xFE, 0xFF], "utf-32be-bom"),
            ([0xFF, 0xFE, 0x00, 0x00], "utf-32le-bom"),
            ([0xEF, 0xBB, 0xBF], "utf-8-bom"),
            ([0xFE, 0xFF], "utf-16be-bom"),
            ([0xFF, 0xFE], "utf-16le-bom")
        ];

        foreach ((byte[] bom, string id) in signatures)
        {
            if (bytes.StartsWith(bom))
            {
                return (EncodingCatalog.FindById(id), bom.Length);
            }
        }

        return (null, 0);
    }

    private static EncodingOption? DetectBomlessUnicode(byte[] bytes)
    {
        if (TryDecode(bytes, EncodingCatalog.FindById("utf-8")!, out string? utf8) && LooksLikeText(utf8))
        {
            return EncodingCatalog.FindById("utf-8");
        }

        if (bytes.Length >= 4)
        {
            int groups = Math.Min(bytes.Length / 4, 4096);
            int leZeros = 0;
            int beZeros = 0;
            for (int i = 0; i < groups; i++)
            {
                int offset = i * 4;
                if (bytes[offset + 1] == 0 && bytes[offset + 2] == 0 && bytes[offset + 3] == 0)
                {
                    leZeros++;
                }

                if (bytes[offset] == 0 && bytes[offset + 1] == 0 && bytes[offset + 2] == 0)
                {
                    beZeros++;
                }
            }

            if (leZeros > groups * .55)
            {
                return EncodingCatalog.FindById("utf-32le");
            }

            if (beZeros > groups * .55)
            {
                return EncodingCatalog.FindById("utf-32be");
            }
        }

        if (bytes.Length >= 2)
        {
            int pairs = Math.Min(bytes.Length / 2, 8192);
            int evenZeros = 0;
            int oddZeros = 0;
            for (int i = 0; i < pairs; i++)
            {
                evenZeros += bytes[i * 2] == 0 ? 1 : 0;
                oddZeros += bytes[i * 2 + 1] == 0 ? 1 : 0;
            }

            if (oddZeros > pairs * .35 && evenZeros < pairs * .10)
            {
                return EncodingCatalog.FindById("utf-16le");
            }

            if (evenZeros > pairs * .35 && oddZeros < pairs * .10)
            {
                return EncodingCatalog.FindById("utf-16be");
            }
        }

        return null;
    }

    private static bool TryDecode(byte[] bytes, EncodingOption option, out string text)
    {
        try
        {
            text = option.CreateEncoding().GetString(bytes);
            byte[] encoded = option.CreateEncoding().GetBytes(text);
            return encoded.AsSpan().SequenceEqual(bytes);
        }
        catch
        {
            text = string.Empty;
            return false;
        }
    }

    private static bool LooksLikeText(string text)
    {
        if (text.Length == 0)
        {
            return true;
        }

        int controls = 0;
        foreach (char character in text)
        {
            if (char.IsControl(character) && character is not ('\r' or '\n' or '\t' or '\f' or '\b'))
            {
                controls++;
            }
        }

        return controls <= Math.Max(2, text.Length / 100);
    }

    private static bool LooksBinary(ReadOnlySpan<byte> bytes)
    {
        int controls = 0;
        int zeros = 0;
        foreach (byte value in bytes)
        {
            zeros += value == 0 ? 1 : 0;
            if (value < 0x09 || value is > 0x0D and < 0x20)
            {
                controls++;
            }
        }

        return zeros > bytes.Length / 100 || controls > Math.Max(4, bytes.Length / 20);
    }

    private static string DetectLineEnding(ReadOnlySpan<byte> bytes, EncodingOption option)
    {
        try
        {
            return DetectLineEnding(option.CreateEncoding().GetString(bytes));
        }
        catch
        {
            return "—";
        }
    }

    private static string DetectLineEnding(string text)
    {
        int crlf = 0;
        int lf = 0;
        int cr = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
            else if (text[i] == '\n')
            {
                lf++;
            }
        }

        int kinds = (crlf > 0 ? 1 : 0) + (lf > 0 ? 1 : 0) + (cr > 0 ? 1 : 0);
        if (kinds == 0)
        {
            return "无";
        }

        if (kinds > 1)
        {
            return "混合";
        }

        return crlf > 0 ? "CRLF" : lf > 0 ? "LF" : "CR";
    }
}

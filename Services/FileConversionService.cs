using System.Text;
using CharsetFlow.Models;

namespace CharsetFlow.Services;

internal sealed record ConversionOptions(
    EncodingOption TargetEncoding,
    LineEndingMode LineEnding,
    OutputMode OutputMode,
    string OutputDirectory,
    bool CreateBackup,
    bool VerifyRoundTrip);

internal sealed record ConversionResult(bool Success, bool Skipped, string OutputPath, string? Error = null);

internal sealed class FileConversionService
{
    public async Task<ConversionResult> ConvertAsync(
        FileItem item,
        ConversionOptions options,
        CancellationToken cancellationToken)
    {
        string outputPath = ResolveOutputPath(item, options);
        string? temporaryPath = null;

        try
        {
            byte[] inputBytes = await File.ReadAllBytesAsync(item.FullPath, cancellationToken);
            string text;
            if (item.IsEmpty)
            {
                text = string.Empty;
            }
            else if (item.SourceEncoding is null)
            {
                throw new InvalidOperationException("未能识别源编码，请先右键手动指定。");
            }
            else
            {
                ReadOnlySpan<byte> content = EncodingDetectionService.RemovePreamble(inputBytes, item.SourceEncoding);
                text = item.SourceEncoding.CreateEncoding().GetString(content);
            }

            cancellationToken.ThrowIfCancellationRequested();
            text = ConvertLineEndings(text, options.LineEnding);
            byte[] body = options.TargetEncoding.CreateEncoding().GetBytes(text);
            byte[] preamble = options.TargetEncoding.GetPreamble();
            byte[] outputBytes = new byte[preamble.Length + body.Length];
            preamble.CopyTo(outputBytes, 0);
            body.CopyTo(outputBytes, preamble.Length);

            if (options.VerifyRoundTrip)
            {
                string roundTrip = options.TargetEncoding.CreateEncoding().GetString(body);
                if (!string.Equals(text, roundTrip, StringComparison.Ordinal))
                {
                    throw new EncoderFallbackException("目标编码无法无损表示文件中的全部字符。");
                }
            }

            if (options.OutputMode == OutputMode.InPlace && inputBytes.AsSpan().SequenceEqual(outputBytes))
            {
                return new ConversionResult(true, true, item.FullPath);
            }

            string? outputDirectory = Path.GetDirectoryName(outputPath);
            if (outputDirectory is null)
            {
                throw new IOException("无法确定输出目录。");
            }

            Directory.CreateDirectory(outputDirectory);
            temporaryPath = Path.Combine(outputDirectory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.charsetflow.tmp");
            await File.WriteAllBytesAsync(temporaryPath, outputBytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (options.OutputMode == OutputMode.InPlace && options.CreateBackup)
            {
                string backupPath = GetAvailableBackupPath(item.FullPath);
                File.Copy(item.FullPath, backupPath, false);
            }

            File.Move(temporaryPath, outputPath, true);
            temporaryPath = null;
            return new ConversionResult(true, false, outputPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ConversionResult(false, false, outputPath, exception.Message);
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // Best effort cleanup after a failed or cancelled conversion.
                }
            }
        }
    }

    private static string ResolveOutputPath(FileItem item, ConversionOptions options)
    {
        if (options.OutputMode == OutputMode.InPlace)
        {
            return item.FullPath;
        }

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            throw new InvalidOperationException("请选择输出文件夹。");
        }

        string root = Path.GetFullPath(options.OutputDirectory);
        string path = Path.GetFullPath(Path.Combine(root, item.RelativePath));
        string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("输出路径超出了目标文件夹。");
        }

        return path;
    }

    private static string ConvertLineEndings(string text, LineEndingMode mode)
    {
        if (mode == LineEndingMode.Preserve || text.Length == 0)
        {
            return text;
        }

        string newline = mode switch
        {
            LineEndingMode.CrLf => "\r\n",
            LineEndingMode.Lf => "\n",
            LineEndingMode.Cr => "\r",
            _ => Environment.NewLine
        };

        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", newline, StringComparison.Ordinal);
    }

    private static string GetAvailableBackupPath(string sourcePath)
    {
        string candidate = sourcePath + ".bak";
        for (int index = 2; File.Exists(candidate); index++)
        {
            candidate = sourcePath + $".bak{index}";
        }

        return candidate;
    }
}

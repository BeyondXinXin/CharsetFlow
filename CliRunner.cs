using System.Runtime.InteropServices;
using System.Text;
using CharsetFlow.Models;
using CharsetFlow.Services;

namespace CharsetFlow;

internal static class CliRunner
{
    private const string Usage = """
        CharsetFlow --help [charset]
        CharsetFlow --input <path>... --target_charset <charset> [--target_linebreak <linebreak>] [--output_origin | --output_dir <dir>]

        --input <path>...                  输入文件或文件夹
        --target_charset <charset>         目标字符编码，例如 UTF-8、GB18030
        --target_linebreak <linebreak>     LF/Linux 或 CRLF/Windows
        --output_origin                    覆盖原文件
        --output_dir <dir>                 输出到指定文件夹
        --help charset                     列出支持的字符编码
        """;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0].Equals("--help", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length > 1 && args[1].Equals("charset", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("支持的字符编码：");
                foreach (EncodingOption option in EncodingCatalog.All)
                {
                    Console.WriteLine(option.DisplayName);
                }
            }
            else
            {
                Console.WriteLine(Usage);
            }

            return 0;
        }

        List<string> inputs = [];
        EncodingOption? targetEncoding = null;
        LineEndingMode lineEnding = LineEndingMode.Preserve;
        OutputMode? outputMode = null;
        string outputDirectory = string.Empty;

        try
        {
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                switch (argument.ToLowerInvariant())
                {
                    case "--input":
                        index++;
                        while (index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal))
                        {
                            inputs.Add(args[index]);
                            index++;
                        }

                        index--;
                        break;

                    case "--target_charset":
                        targetEncoding = EncodingCatalog.Resolve(RequireValue(args, ref index, argument));
                        if (targetEncoding is null)
                        {
                            throw new ArgumentException("无法识别目标字符编码。可使用 --help charset 查看名称。");
                        }

                        break;

                    case "--target_linebreak":
                        lineEnding = ParseLineEnding(RequireValue(args, ref index, argument));
                        break;

                    case "--output_origin":
                        outputMode = OutputMode.InPlace;
                        break;

                    case "--output_dir":
                        outputMode = OutputMode.Directory;
                        outputDirectory = RequireValue(args, ref index, argument);
                        break;

                    default:
                        throw new ArgumentException($"无效参数：{argument}");
                }
            }

            if (inputs.Count == 0)
            {
                throw new ArgumentException("没有设置输入路径（--input）。");
            }

            if (targetEncoding is null)
            {
                throw new ArgumentException("没有设置目标字符编码（--target_charset）。");
            }

            if (outputMode is null)
            {
                throw new ArgumentException("没有设置输出方式（--output_origin 或 --output_dir）。");
            }
        }
        catch (ArgumentException exception)
        {
            WriteError(exception.Message);
            Console.WriteLine();
            Console.WriteLine(Usage);
            return 2;
        }

        using CancellationTokenSource cancellation = new();
        try
        {
            Console.CancelKeyPress += Cancel;
        }
        catch
        {
            // Some redirected hosts do not expose a console cancellation event.
        }

        try
        {
            EncodingDetectionService detector = new();
            FileScanner scanner = new(detector);
            IReadOnlyList<FileItem> files = await scanner.ScanAsync(
                inputs,
                new ScanOptions(FilterMode.All, string.Empty, string.Empty, true),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                null,
                cancellation.Token);

            Console.WriteLine($"发现 {files.Count} 个文件，目标编码：{targetEncoding.DisplayName}");
            FileConversionService converter = new();
            ConversionOptions options = new(
                targetEncoding,
                lineEnding,
                outputMode.Value,
                outputDirectory,
                false,
                true);

            int success = 0;
            int skipped = 0;
            int failed = 0;
            for (int index = 0; index < files.Count; index++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                FileItem file = files[index];
                Console.Write($"[{index + 1}/{files.Count}] {file.FullPath}  {file.EncodingName} → {targetEncoding.DisplayName}  ");
                ConversionResult result = await converter.ConvertAsync(file, options, cancellation.Token);
                if (!result.Success)
                {
                    failed++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"失败：{result.Error}");
                }
                else if (result.Skipped)
                {
                    skipped++;
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("无需转换");
                }
                else
                {
                    success++;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("完成");
                }

                Console.ResetColor();
            }

            Console.WriteLine($"处理完成：成功 {success}，跳过 {skipped}，失败 {failed}");
            return failed == 0 ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            WriteError("操作已取消。");
            return 130;
        }

        void Cancel(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }
    }

    private static string RequireValue(string[] args, ref int index, string argument)
    {
        if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"参数 {argument} 缺少值。");
        }

        return args[index];
    }

    private static LineEndingMode ParseLineEnding(string value) => value.ToLowerInvariant() switch
    {
        "lf" or "linux" or "unix" => LineEndingMode.Lf,
        "crlf" or "windows" => LineEndingMode.CrLf,
        _ => throw new ArgumentException($"无法识别换行符：{value}")
    };

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ResetColor();
    }
}

internal static class ConsoleBridge
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    public static void EnsureConsole()
    {
        if (GetConsoleWindow() == IntPtr.Zero)
        {
            _ = AttachConsole(AttachParentProcess);
        }

        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            StreamWriter output = new(Console.OpenStandardOutput()) { AutoFlush = true };
            StreamWriter error = new(Console.OpenStandardError()) { AutoFlush = true };
            Console.SetOut(output);
            Console.SetError(error);
        }
        catch
        {
            // GUI launchers can run CLI arguments without an attachable parent console.
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
}

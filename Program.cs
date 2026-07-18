using System.Text;

namespace CharsetFlow;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        if (args.Any(argument => argument.StartsWith("--", StringComparison.Ordinal)))
        {
            ConsoleBridge.EnsureConsole();
            return CliRunner.RunAsync(args).GetAwaiter().GetResult();
        }

        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(new Font("Segoe UI Variable Text", 9F));
        Application.Run(new MainForm(args));
        return 0;
    }
}

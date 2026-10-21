namespace Transcriber;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppSettings.ConfigureRuntime(AppSettings.Load().ComputeDevice);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

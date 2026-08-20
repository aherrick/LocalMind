using Velopack;
using LocalMind.Services;

namespace LocalMind;

// WinUI normally generates this entry point for us. We supply our own so Velopack can initialize
// before the XAML framework starts; `DISABLE_XAML_GENERATED_MAIN` (in the .csproj) suppresses the
// generated one, which would otherwise be a second Main (CS0017).
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Must be the first code in Main. During install/update/uninstall, Velopack relaunches this exe
        // with hook arguments and exits from inside Run() - anything above it would run during those
        // operations too, which is what crashed the XAML runtime (0xc000027b) when this lived in App's ctor.
        VelopackApp.Build().Run();

        AppLog.Initialize();
        AppLog.Info($"Log directory: {AppLog.DirectoryPath}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppLog.Error("Unhandled app domain exception.", ex);
            }
            else
            {
                AppLog.Info($"Unhandled app domain exception object: {e.ExceptionObject}");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("Unobserved task exception.", e.Exception);
            e.SetObserved();
        };

        // Suppressing the generated Main doesn't delete its body, it just renames it to this callable
        // method: InitializeComWrappers() -> Application.Start() -> new App(). Delegating keeps that
        // boilerplate owned by the SDK instead of hand-copied here.
        XamlGeneratedProgram.XamlGeneratedMain();
    }
}

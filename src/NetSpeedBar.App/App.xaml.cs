using System.Threading;

namespace NetSpeedBar.App;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "NetSpeedBar-2d096a9f-ae70-47d9-a1bc-0a431e50506e", out var created);
        if (!created)
        {
            System.Windows.MessageBox.Show("NetSpeedBar は既に起動しています。", "NetSpeedBar");
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }
    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}

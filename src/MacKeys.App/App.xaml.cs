using System.Windows;

namespace MacKeys;

public partial class App : Application
{
    public const string ProductName = "Mac Keys for Windows";

    private MainWindow? _window;

    public static bool IsExiting { get; set; }

    public App()
    {
        InitializeComponent();
        Theme.Apply(this);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // This process does not install a keyboard hook. PowerToys Keyboard Manager does.
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!SingleInstance.TryBecomePrimary(() => Dispatcher.BeginInvoke(BringToFront)))
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _window = new MainWindow();
        MainWindow = _window;
        _window.Show();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _window?.ExitApplication(interactive: false);
        base.OnSessionEnding(e);
    }

    private void BringToFront() => _window?.BringToFront();
}

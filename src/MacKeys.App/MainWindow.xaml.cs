using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MacKeys;

public partial class MainWindow : Window
{
    private readonly string _settingsPath = AppSettingsStore.DefaultPath;
    private readonly StartupRegistration _startup = new();
    private readonly KeyboardManagerStore _store = new(KeyboardManagerStore.DefaultDirectory);
    private readonly DispatcherTimer _timer;
    private AppSettings _settings = new();
    private ProfileSession _session = null!;
    private TrayIcon? _tray;
    private PowerToysInstallation _powerToys = PowerToysInstallation.Missing;
    private bool _engineRunning;
    private bool _loadingUi;
    private bool _checkingInstall;
    private int _installTicks;
    private int _exitOnce;
    private ForegroundObservation _foreground = ForegroundObservation.Unavailable;

    public MainWindow()
    {
        InitializeComponent();
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(AppIcon.Bitmap);
        AlwaysList.ItemsSource = new[] { RemapCatalog.AlwaysAppliedDisplay };
        ShortcutList.ItemsSource = RemapCatalog.Shortcuts;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Profiles.ForegroundPollMilliseconds) };
        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) => Start();
    }

    public void BringToFront()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void ExitApplication(bool interactive = true)
    {
        if (Interlocked.Exchange(ref _exitOnce, 1) == 1)
            return;

        App.IsExiting = true;
        _timer.Stop();
        try
        {
            _session?.Exit();
        }
        catch (Exception exception)
        {
            if (interactive)
                MessageBox.Show(this, exception.Message, "Mac Keys", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _tray?.Dispose();
        _tray = null;
        Application.Current.Shutdown();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!App.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void Start()
    {
        var loaded = AppSettingsStore.Load(_settingsPath);
        _settings = loaded.Settings;
        _session = new ProfileSession(_store, SettingsChangedEvent.Signal, _settings.Disabled, _settings.Games);
        BindGames();

        _loadingUi = true;
        var startupEnabled = _startup.IsEnabled();
        StartWithWindowsCheck.IsChecked = startupEnabled;
        _loadingUi = false;
        if (startupEnabled)
        {
            try
            {
                _startup.Enable(StartupRegistration.CurrentExecutable());
            }
            catch (Exception exception)
            {
                SetAction(exception.Message);
            }
        }

        _tray = new TrayIcon
        {
            IsSuspended = () => _session.Suspended,
        };
        _tray.OpenRequested += () => Dispatcher.Invoke(BringToFront);
        _tray.ApplyRequested += () => Dispatcher.Invoke(ApplyFromHome);
        _tray.SuspendToggleRequested += () => Dispatcher.Invoke(TurnOff);
        _tray.ExitRequested += () => Dispatcher.Invoke(() => ExitApplication());

        RefreshPowerToys(allowWinget: false);
        if (!_powerToys.Installed)
            CheckInstallWithWinget();

        UpdateSuspendButton();
        if (loaded.Error != null)
            SetAction(loaded.Error);

        try
        {
            _foreground = ForegroundProcess.Query();
            var result = _session.ApplyNow(_foreground);
            SetAction("시작하면서 프로필을 다시 썼습니다. 현재: " + ModeName(result.Profile) + ".");
        }
        catch (Exception exception)
        {
            SetAction(exception.Message);
        }

        Render();
        _timer.Start();
        EnsureContentFits();
    }

    private void Tick()
    {
        try
        {
            _foreground = ForegroundProcess.Query();
            _session.OnForeground(_foreground);
        }
        catch (Exception exception)
        {
            SetAction(exception.Message);
        }

        if (++_installTicks >= 5)
        {
            _installTicks = 0;
            if (!_checkingInstall)
                RefreshPowerToys(allowWinget: false);
            _engineRunning = PowerToysLocator.IsEngineRunning();
        }

        Render();
    }

    private void ApplyClick(object sender, RoutedEventArgs e) => ApplyFromHome();

    private void ApplyFromHome()
    {
        try
        {
            if (_settings.Disabled)
            {
                _settings.Disabled = false;
                AppSettingsStore.Save(_settingsPath, _settings);
                _foreground = ForegroundProcess.Query();
                _session.SetSuspended(false, _foreground);
                UpdateSuspendButton();
            }

            ApplyProfiles();
        }
        catch (Exception exception)
        {
            SetAction(exception.Message);
            UpdateSuspendButton();
            Render();
        }
    }

    private void ApplyProfiles()
    {
        try
        {
            _foreground = ForegroundProcess.Query();
            var result = _session.ApplyNow(_foreground);
            SetAction("프로필을 다시 썼습니다. 현재: " + ModeName(result.Profile) + ".");
            Render();
        }
        catch (Exception exception)
        {
            SetAction(exception.Message);
        }
    }

    private void SuspendClick(object sender, RoutedEventArgs e) => TurnOff();

    private void TurnOff()
    {
        if (!_settings.Disabled)
            ToggleSuspended();
    }

    private void ToggleSuspended()
    {
        var next = !_settings.Disabled;
        _settings.Disabled = next;
        try
        {
            AppSettingsStore.Save(_settingsPath, _settings);
            _foreground = ForegroundProcess.Query();
            var result = _session.SetSuspended(next, _foreground);
            SetAction(next
                ? "껐습니다. 키는 Windows 그대로입니다."
                : "적용했습니다. 현재: " + ModeName(result.Profile) + ".");
        }
        catch (Exception exception)
        {
            _settings.Disabled = !next;
            SetAction(exception.Message);
        }

        UpdateSuspendButton();
        Render();
    }

    private void OpenSettingsClick(object sender, RoutedEventArgs e)
    {
        if (!_powerToys.CanOpenSettings || _powerToys.ExecutablePath == null)
        {
            SetAction("PowerToys.exe를 찾지 못해 설정 창을 열지 못했습니다.");
            return;
        }

        try
        {
            // The settings pipe exists only while the settings window is open, so this does not enable the module itself.
            PowerToysLocator.OpenKeyboardManagerSettings(_powerToys.ExecutablePath);
        }
        catch (Exception exception)
        {
            SetAction(exception.Message);
        }
    }

    private void WingetClick(object sender, RoutedEventArgs e)
    {
        try
        {
            PowerToysLocator.StartWingetInstall();
            SetAction("winget 설치 창을 열었습니다.");
        }
        catch (Exception exception)
        {
            SetAction("winget을 시작하지 못했습니다. " + exception.Message);
        }
    }

    private void StoreClick(object sender, RoutedEventArgs e)
    {
        try
        {
            PowerToysLocator.OpenStore();
        }
        catch (Exception exception)
        {
            SetAction(exception.Message);
        }
    }

    private void AddRunningClick(object sender, RoutedEventArgs e)
    {
        var picker = new ProcessPickerWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.Selected == null)
            return;

        UpsertGame(picker.Selected.ProcessName, picker.Selected.Path);
    }

    private void AddFileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "게임 실행 파일 선택",
            Filter = "프로그램 (*.exe)|*.exe",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true)
            return;

        UpsertGame(System.IO.Path.GetFileName(dialog.FileName), dialog.FileName);
    }

    private void RemoveGameClick(object sender, RoutedEventArgs e)
    {
        if (GamesView.SelectedItem is not GameRow row)
        {
            SetAction("제거할 게임을 고르세요.");
            return;
        }

        _settings.Games = GameList.Remove(_settings.Games, row.ProcessName).ToList();
        PersistGames("목록에서 뺐습니다.");
    }

    private void UpsertGame(string processName, string? path)
    {
        try
        {
            var change = GameList.Upsert(_settings.Games, processName, path);
            _settings.Games = change.Games.ToList();
            PersistGames(change.UpdatedExisting
                ? "이미 있는 이름입니다. 경로를 갱신했습니다."
                : "목록에 추가했습니다. 그 창이 전경일 때 게임 프로필을 씁니다.");
        }
        catch (Exception exception)
        {
            SetAction(exception.Message);
        }
    }

    private void PersistGames(string message)
    {
        AppSettingsStore.Save(_settingsPath, _settings);
        _session.UpdateGames(_settings.Games);
        _foreground = ForegroundProcess.Query();
        _session.OnForeground(_foreground);
        SetAction(message);
        BindGames();
        Render();
    }

    private void StartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingUi)
            return;

        try
        {
            if (StartWithWindowsCheck.IsChecked == true)
                _startup.Enable(StartupRegistration.CurrentExecutable());
            else
                _startup.Disable();
        }
        catch (Exception exception)
        {
            SetAction(exception.Message);
        }
    }

    private void BindGames()
    {
        var selected = (GamesView.SelectedItem as GameRow)?.ProcessName;
        GamesView.ItemsSource = _settings.Games
            .Select(game => new GameRow(game.ProcessName, string.IsNullOrWhiteSpace(game.Path) ? "경로를 읽지 못함" : game.Path))
            .ToList();
        if (selected != null)
        {
            GamesView.SelectedItem = GamesView.Items
                .OfType<GameRow>()
                .FirstOrDefault(row => ProcessNames.Equal(row.ProcessName, selected));
        }

        EmptyGamesText.Visibility = _settings.Games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EnsureContentFits();
    }

    private bool _fitting;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_fitting || !IsLoaded || !sizeInfo.WidthChanged)
            return;

        FitWindowToContent();
    }

    private void EnsureContentFits()
    {
        if (!IsLoaded)
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FitWindowToContent);
    }

    private void FitWindowToContent()
    {
        if (_fitting)
            return;

        _fitting = true;
        try
        {
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var longest = 0.0;
            foreach (var shortcut in RemapCatalog.Shortcuts)
            {
                var formatted = new FormattedText(
                    $"{shortcut.From} → {shortcut.To}   {shortcut.Meaning}",
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    11,
                    Brushes.Black,
                    dpi);
                if (formatted.WidthIncludingTrailingWhitespace > longest)
                    longest = formatted.WidthIncludingTrailingWhitespace;
            }

            const double columnGap = 16;
            var leftCard = longest + 24 + 16;
            var panes = leftCard * 3;
            var minWidth = Math.Ceiling(panes + columnGap + SettingsRoot.Margin.Left + SettingsRoot.Margin.Right + 24);
            if (minWidth < 1040)
                minWidth = 1040;
            if (MinWidth < minWidth)
                MinWidth = minWidth;
            if (Width < MinWidth)
                Width = MinWidth;

            var slot = Math.Max(MinWidth, Width) - SettingsRoot.Margin.Left - SettingsRoot.Margin.Right - 16;
            SettingsRoot.Measure(new Size(Math.Max(slot, 0), double.PositiveInfinity));
            var measured = SettingsRoot.DesiredSize.Height + SettingsRoot.Margin.Top + SettingsRoot.Margin.Bottom;
            SettingsRoot.InvalidateMeasure();

            var rows = (RemapCatalog.Shortcuts.Count + 1) / 2;
            var shortcutCard = 130 + rows * 40;
            var gamesCard = 210 + _settings.Games.Count * 44;
            var conservative = 120 + Math.Max(shortcutCard, gamesCard) + 160 + 52 + 40;
            double caption = 32;
            if (Content is FrameworkElement root && root.ActualHeight > 1 && ActualHeight > root.ActualHeight + 8)
                caption = ActualHeight - root.ActualHeight;

            var needed = measured + 52 + caption;
            if (double.IsNaN(needed) || double.IsInfinity(needed) || measured < 400)
                needed = conservative;
            needed = Math.Ceiling(needed);
            MinHeight = needed;
            if (Height < needed)
                Height = needed;
        }
        finally
        {
            _fitting = false;
        }
    }

    private void SetAction(string? message)
    {
        var text = message ?? "";
        ActionStatusText.Text = text;
        HomeActionText.Text = text;
    }

    private void RefreshPowerToys(bool allowWinget)
    {
        _powerToys = PowerToysLocator.Detect(new PowerToysDetectOptions(null, true, true, allowWinget));
        _engineRunning = PowerToysLocator.IsEngineRunning();
    }

    private void CheckInstallWithWinget()
    {
        _checkingInstall = true;
        InstallStatusText.Text = "설치 여부를 확인하는 중입니다.";
        Task.Run(() =>
        {
            var found = PowerToysLocator.Detect();
            Dispatcher.Invoke(() =>
            {
                _checkingInstall = false;
                _powerToys = found;
                _engineRunning = PowerToysLocator.IsEngineRunning();
                Render();
            });
        });
    }

    private void Render()
    {
        var installed = _powerToys.Installed;
        InstallStatusText.Text = _checkingInstall
            ? "설치 여부를 확인하는 중입니다."
            : installed
                ? _powerToys.CanOpenSettings
                    ? "PowerToys가 설치되어 있습니다."
                    : "PowerToys 등록은 있지만 PowerToys.exe를 찾지 못했습니다."
                : "PowerToys가 설치되어 있지 않습니다.";
        InstallStatusText.Foreground = Paint(installed ? "OkBrush" : "DangerBrush");
        ExePathText.Text = _powerToys.ExecutablePath ?? "";
        EngineStatusText.Text = _engineRunning
            ? "Keyboard Manager 엔진이 실행 중입니다."
            : "Keyboard Manager 엔진이 꺼져 있습니다. 모듈 기본값은 꺼짐이라, 설정 파일만 있고 키는 바뀌지 않습니다. 설정 창을 열어 Keyboard Manager를 켜 주세요.";
        EngineStatusText.Foreground = Paint(_engineRunning ? "OkBrush" : "DangerBrush");
        OpenSettingsButton.IsEnabled = _powerToys.CanOpenSettings;
        InstallPanel.Visibility = _powerToys.CanOpenSettings ? Visibility.Collapsed : Visibility.Visible;

        var profile = _session.ActiveProfile;
        var on = !_session.Suspended && profile != Profiles.Off;
        PowerHeadline.Text = on ? "적용 중" : "꺼짐";
        PowerHeadline.Foreground = Paint(on ? "OkBrush" : "TextMuted");
        ProfileStatusText.Text = "현재: " + ModeName(profile);
        HomeEngineText.Text = EngineStatusText.Text;
        HomeEngineText.Visibility = _engineRunning ? Visibility.Collapsed : Visibility.Visible;

        ForegroundStatusText.Text = DescribeForeground(_foreground, _session.Suspended);
        if (_tray != null && profile != null)
            _tray.SetTooltip("Mac Keys — " + ModeName(profile));
    }

    private void UpdateSuspendButton()
    {
        var on = !_settings.Disabled;
        ApplyButton.Style = (Style)FindResource(on ? "HomeButton" : "HomePrimaryButton");
        SuspendButton.Style = (Style)FindResource(on ? "HomePrimaryButton" : "HomeButton");
    }

    private Brush Paint(string key) => (Brush)FindResource(key);

    private static string ModeName(string? profile) => profile switch
    {
        Profiles.Daily => "맥단축키",
        Profiles.Game => "게임 중(한영전환만)",
        Profiles.Off => "꺼짐(Windows)",
        _ => "아직 없음",
    };

    private static string DescribeForeground(ForegroundObservation foreground, bool suspended)
    {
        var line = foreground.Status switch
        {
            ForegroundReadStatus.Known => "전경: " + foreground.ProcessName,
            ForegroundReadStatus.AccessDenied => "관리자 권한 창이라 프로세스 이름을 읽지 못했습니다. 게임 프로필로 바꾸지 않습니다.",
            _ => "전경 창의 프로세스 이름을 읽지 못했습니다. 게임 프로필로 바꾸지 않습니다.",
        };

        if (suspended)
            line += "  꺼져 있는 동안에는 전경 창을 봐도 바꾸지 않습니다.";
        return line;
    }

    private sealed record GameRow(string ProcessName, string PathText);
}

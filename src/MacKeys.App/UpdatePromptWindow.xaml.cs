using System.IO;
using System.Windows;

namespace MacKeys;

public enum UpdateChoice
{
    Later,
    Skip,
    Install,
}

public partial class UpdatePromptWindow : Window
{
    private readonly UpdateOffer _offer;
    private readonly CancellationTokenSource _cancel = new();

    public UpdatePromptWindow(string currentVersion, UpdateOffer offer)
    {
        _offer = offer;
        InitializeComponent();
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(AppIcon.Bitmap);
        HeadlineText.Text = "새 버전 " + offer.Version;
        CurrentText.Text = "현재 버전은 " + currentVersion + " 입니다.";
        NotesText.Text = offer.Notes;
        if (offer.DownloadUrl == null)
        {
            UpdateButton.IsEnabled = false;
            StatusText.Text = "설치 파일이 없습니다.";
        }
    }

    public UpdateChoice Choice { get; private set; } = UpdateChoice.Later;

    public string? DownloadedPath { get; private set; }

    private void SkipClick(object sender, RoutedEventArgs e)
    {
        Choice = UpdateChoice.Skip;
        DialogResult = true;
    }

    private async void UpdateClick(object sender, RoutedEventArgs e)
    {
        if (_offer.DownloadUrl == null)
            return;

        SetBusy(true);
        StatusText.Text = "받는 중...";
        try
        {
            UpdateInstaller.EnsureCanReplace(StartupRegistration.CurrentExecutable());
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
            SetBusy(false);
            return;
        }

        var fileName = SafeFileName(Path.GetFileName(new Uri(_offer.DownloadUrl).LocalPath));
        var destination = Path.Combine(Path.GetTempPath(), "MacKeys", "update", fileName);
        try
        {
            await UpdateClient.DownloadAsync(
                _offer.DownloadUrl,
                destination,
                new Progress<DownloadProgress>(progress => StatusText.Text = Describe(progress)),
                _cancel.Token);
            DownloadedPath = destination;
            Choice = UpdateChoice.Install;
            StatusText.Text = "설치하고 다시 시작합니다.";
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            TryDelete(destination);
        }
        catch (Exception exception)
        {
            TryDelete(destination);
            StatusText.Text = exception.Message;
            SetBusy(false);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancel.Cancel();
        base.OnClosed(e);
    }

    private void SetBusy(bool busy)
    {
        SkipButton.IsEnabled = !busy;
        UpdateButton.IsEnabled = !busy && _offer.DownloadUrl != null;
        LaterButton.IsEnabled = !busy;
    }

    private static string Describe(DownloadProgress progress)
    {
        if (progress.Total is not long total || total <= 0)
            return "받는 중...";

        var percent = (int)Math.Min(100, progress.Received * 100 / total);
        return "받는 중... " + percent + "%";
    }

    private static string SafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "MacKeys-update.exe";
        return name;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}

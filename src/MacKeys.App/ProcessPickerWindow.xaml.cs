using System.Windows;
using System.Windows.Input;

namespace MacKeys;

public partial class ProcessPickerWindow : Window
{
    public ProcessPickerWindow()
    {
        InitializeComponent();
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(AppIcon.Bitmap);
        try
        {
            Rows.ItemsSource = RunningProcesses.List()
                .Select(row => new Row(row))
                .ToList();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, App.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public RunningProcessRow? Selected { get; private set; }

    private void AddClick(object sender, RoutedEventArgs e) => Accept();

    private void RowDoubleClick(object sender, MouseButtonEventArgs e) => Accept();

    private void Accept()
    {
        if (Rows.SelectedItem is not Row row)
        {
            MessageBox.Show(this, "추가할 프로세스를 고르세요.", App.ProductName, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Selected = row.Source;
        DialogResult = true;
    }

    private sealed class Row
    {
        public Row(RunningProcessRow source)
        {
            Source = source;
        }

        public RunningProcessRow Source { get; }
        public string ProcessName => Source.ProcessName;
        public string WindowTitle => Source.WindowTitle ?? "";
        public string PathText => Source.Path ?? "경로를 읽지 못함";
    }
}

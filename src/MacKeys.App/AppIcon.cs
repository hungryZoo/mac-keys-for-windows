using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MacKeys;

internal static class AppIcon
{
    public static BitmapSource Bitmap { get; } = Draw();

    public static IntPtr CreateIcon() => CreateHIcon(Bitmap);

    private static BitmapSource Draw()
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var background = new SolidColorBrush(Color.FromRgb(47, 111, 237));
            context.DrawRoundedRectangle(background, null, new Rect(0, 0, size, size), 7, 7);
            var text = new FormattedText(
                "\u2318",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Symbol"),
                18,
                Brushes.White,
                1.0);
            context.DrawText(text, new Point((size - text.Width) / 2, (size - text.Height) / 2));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static IntPtr CreateHIcon(BitmapSource source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        var header = new BitmapInfoHeader
        {
            biSize = Marshal.SizeOf<BitmapInfoHeader>(),
            biWidth = width,
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32,
        };

        var screen = GetDC(IntPtr.Zero);
        var color = CreateDIBSection(screen, ref header, 0, out var bits, IntPtr.Zero, 0);
        if (color == IntPtr.Zero)
        {
            ReleaseDC(IntPtr.Zero, screen);
            return IntPtr.Zero;
        }

        Marshal.Copy(pixels, 0, bits, pixels.Length);
        var mask = CreateBitmap(width, height, 1, 1, IntPtr.Zero);
        var info = new IconInfo
        {
            fIcon = true,
            hbmColor = color,
            hbmMask = mask,
        };
        var icon = CreateIconIndirect(ref info);
        DeleteObject(color);
        DeleteObject(mask);
        ReleaseDC(IntPtr.Zero, screen);
        return icon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr dc,
        ref BitmapInfoHeader header,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitCount, IntPtr bits);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateIconIndirect(ref IconInfo info);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}

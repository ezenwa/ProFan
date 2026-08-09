using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

internal static class IconGenerator
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    public static void Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : "ProFan.ico";
        using (var bitmap = new Bitmap(64, 64))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using (var background = new SolidBrush(Color.FromArgb(28, 28, 33))) graphics.FillEllipse(background, 2, 2, 60, 60);
            using (var ring = new Pen(Color.FromArgb(0, 174, 239), 3)) graphics.DrawEllipse(ring, 4, 4, 56, 56);
            graphics.TranslateTransform(32, 32);
            using (var blade = new SolidBrush(Color.FromArgb(0, 174, 239)))
            {
                for (int i = 0; i < 5; i++)
                {
                    graphics.FillEllipse(blade, -3, -25, 14, 25);
                    graphics.RotateTransform(72);
                }
            }
            using (var hub = new SolidBrush(Color.FromArgb(245, 245, 248))) graphics.FillEllipse(hub, -7, -7, 14, 14);
            graphics.ResetTransform();
            IntPtr hIcon = bitmap.GetHicon();
            try
            {
                using (var icon = Icon.FromHandle(hIcon))
                using (var stream = File.Create(output)) icon.Save(stream);
            }
            finally { DestroyIcon(hIcon); }
        }
    }
}

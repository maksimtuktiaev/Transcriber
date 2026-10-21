using System.Drawing;

namespace Transcriber;

internal sealed record ThemePalette(
    Color Window, Color Surface, Color SurfaceAlt, Color Text, Color Muted,
    Color Border, Color Accent, Color AccentText, Color Input)
{
    public static ThemePalette For(AppTheme theme)
    {
        bool dark = theme == AppTheme.Dark ||
                    (theme == AppTheme.System &&
                     Microsoft.Win32.Registry.GetValue(
                         @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                         "AppsUseLightTheme", 1) is int light && light == 0);

        return dark
            ? new(Color.FromArgb(20, 22, 27), Color.FromArgb(29, 32, 39), Color.FromArgb(37, 41, 49),
                Color.FromArgb(239, 241, 245), Color.FromArgb(157, 164, 176), Color.FromArgb(55, 60, 70),
                Color.FromArgb(86, 156, 255), Color.White, Color.FromArgb(24, 27, 33))
            : new(Color.FromArgb(246, 247, 249), Color.White, Color.FromArgb(239, 242, 246),
                Color.FromArgb(28, 32, 39), Color.FromArgb(103, 111, 123), Color.FromArgb(222, 226, 232),
                Color.FromArgb(33, 105, 220), Color.White, Color.White);
    }
}

using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Gomoku.Services;

/// <summary>
/// 主题服务：明 / 暗 / 跟随系统。
/// 修改应用 RequestedTheme（影响 Mica 与所有 ThemeResource），并广播 ThemeChanged 供棋盘重建画刷。
/// </summary>
public class ThemeService
{
    private UISettings? _ui;
    private bool _systemIsDark;
    private MainWindow? _window;

    public event Action? ThemeChanged;

    public bool SystemIsDark => _systemIsDark;

    public void Init(MainWindow window)
    {
        _window = window;
        try
        {
            _ui = new UISettings();
            _ui.ColorValuesChanged += (_, _) =>
            {
                if (App.Settings.Theme == AppTheme.System)
                {
                    _ = _window.DispatcherQueue.TryEnqueue(() => Apply(AppTheme.System));
                }
            };
        }
        catch { /* 旧系统不支持时仅手动切换 */ }
        Apply(App.Settings.Theme);
    }

    public void Apply(AppTheme theme)
    {
        App.Settings.Theme = theme;
        App.Settings.Save();

        // 注意：Application.RequestedTheme 只能在创建任何窗口之前设置，
        // 窗口存在后再设置会抛 COMException。因此明暗切换统一通过根元素
        // RequestedTheme 控制（ThemeResource 与 Mica 均随之生效）。
        if (theme == AppTheme.System)
        {
            // 跟随系统：读一次系统背景色推断明暗
            try
            {
                var c = _ui!.GetColorValue(UIColorType.Background);
                _systemIsDark = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0 < 0.5;
            }
            catch { _systemIsDark = false; }
        }

        if (_window?.RootGrid != null)
        {
            _window.RootGrid.RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
        }
        ApplyTitleBarColors(theme);
        ThemeChanged?.Invoke();
    }

    public void Toggle()
    {
        var next = App.Settings.Theme switch
        {
            AppTheme.System => SystemIsDark ? AppTheme.Light : AppTheme.Dark,
            AppTheme.Dark => AppTheme.Light,
            _ => AppTheme.Dark,
        };
        Apply(next);
    }

    // ---------- 自定义标题栏按钮颜色（与页面同主题，Mica 透出） ----------

    private void ApplyTitleBarColors(AppTheme theme)
    {
        try
        {
            if (_window?.AppWindow.TitleBar is not { } tb) return;
            bool dark = theme switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                _ => SystemIsDark,
            };
            var bg = Microsoft.UI.Colors.Transparent;   // 透明底，让 Mica 透出标题栏

            tb.ButtonBackgroundColor = bg;
            tb.ButtonInactiveBackgroundColor = bg;
            tb.ButtonForegroundColor = ThemeBrush("TextFillColorPrimaryBrush", dark) ?? (dark ? Colors.White : Color.FromArgb(255, 0x1B, 0x1B, 0x1B));
            tb.ButtonHoverBackgroundColor = ThemeBrush("SubtleFillColorSecondaryBrush", dark) ?? (dark ? Color.FromArgb(255, 0x33, 0x33, 0x33) : Color.FromArgb(255, 0xE5, 0xE5, 0xE5));
            tb.ButtonHoverForegroundColor = tb.ButtonForegroundColor;
            tb.ButtonPressedBackgroundColor = ThemeBrush("SubtleFillColorTertiaryBrush", dark) ?? (dark ? Color.FromArgb(255, 0x4D, 0x4D, 0x4D) : Color.FromArgb(255, 0xD0, 0xD0, 0xD0));
            tb.ButtonPressedForegroundColor = tb.ButtonForegroundColor;
        }
        catch { /* 旧系统 / 无自定义标题栏时忽略 */ }
    }

    /// <summary>从指定明暗主题字典读取画刷颜色（Application 资源按系统主题查找，必须显式指定字典）。</summary>
    private static Color? ThemeBrush(string key, bool dark)
    {
        try
        {
            if (Application.Current.Resources.ThemeDictionaries.TryGetValue(dark ? "Dark" : "Light", out var d)
                && d is ResourceDictionary dict
                && dict.TryGetValue(key, out var v)
                && v is SolidColorBrush b)
                return b.Color;
        }
        catch { }
        return null;
    }
}

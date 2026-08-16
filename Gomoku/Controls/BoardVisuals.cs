using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Gomoku.Game;

namespace Gomoku.Controls;

/// <summary>
/// 棋盘视觉素材：立体棋子画刷等。
/// 全部为静态共享实例 —— 数百颗棋子共用同一批画刷，内存与 GPU 开销极低。
/// </summary>
public static class BoardVisuals
{
    private static RadialGradientBrush? _black, _white;
    private static SolidColorBrush? _blackRim, _whiteRim;
    private static SolidColorBrush? _specular, _accent;

    public static Brush Accent => _accent ??= AccentBrush();

    /// <summary>棋子表面高光（左上角反光点）。</summary>
    public static Brush Specular => _specular ??= new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

    /// <summary>按主题获取棋子主体画刷（径向渐变：左上有光、右下沉底）。</summary>
    public static RadialGradientBrush StoneBrush(StoneColor color, ElementTheme theme)
    {
        Ensure(theme);
        return color == StoneColor.Black ? _black! : _white!;
    }

    /// <summary>棋子边缘暗圈。</summary>
    public static Brush StoneRim(StoneColor color, ElementTheme theme)
    {
        Ensure(theme);
        return color == StoneColor.Black ? _blackRim! : _whiteRim!;
    }

    /// <summary>在主题切换后重建画刷（棋盘在浅色 / 深色下棋子观感微调）。</summary>
    public static void Ensure(ElementTheme theme)
    {
        bool dark = theme == ElementTheme.Dark;
        if (_black != null && _blackDark == dark) return;

        _blackDark = dark;
        if (dark)
        {
            _black = Stone(dark, 0xFF9A9A9A, 0xFF3C3C3C, 0xFF141414, 0xFF070707);
            _white = Stone(dark, 0xFFFFFFFF, 0xFFEFE9DD, 0xFFC8C0B0, 0xFFA79E8C);
        }
        else
        {
            _black = Stone(dark, 0xFF6A6A6A, 0xFF262626, 0xFF0C0C0C, 0xFF020202);
            _white = Stone(dark, 0xFFFFFFFF, 0xFFF0EBDD, 0xFFCFC7B6, 0xFFB0A795);
        }
        _blackRim = new SolidColorBrush(Color.FromArgb(235, 0, 0, 0));
        _whiteRim = new SolidColorBrush(Color.FromArgb(120, 0x7E, 0x76, 0x66));
    }

    private static bool _blackDark;

    private static RadialGradientBrush Stone(bool dark, uint c0, uint c1, uint c2, uint c3)
    {
        var b = new RadialGradientBrush
        {
            Center = new Windows.Foundation.Point(0.5, 0.5),
            GradientOrigin = new Windows.Foundation.Point(0.33, 0.30),
            RadiusX = 0.55,
            RadiusY = 0.55,
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
        };
        b.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, (byte)(c0 >> 16), (byte)(c0 >> 8), (byte)c0), Offset = 0.0 });
        b.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, (byte)(c1 >> 16), (byte)(c1 >> 8), (byte)c1), Offset = 0.45 });
        b.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, (byte)(c2 >> 16), (byte)(c2 >> 8), (byte)c2), Offset = 0.8 });
        b.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, (byte)(c3 >> 16), (byte)(c3 >> 8), (byte)c3), Offset = 1.0 });
        return b;
    }

    private static SolidColorBrush AccentBrush()
    {
        try
        {
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var c) && c is Color col)
                return new SolidColorBrush(col);
        }
        catch { }
        return new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
    }
}

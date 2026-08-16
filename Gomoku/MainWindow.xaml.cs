using System;
using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Gomoku.Pages;

namespace Gomoku;

public sealed partial class MainWindow : Window
{
    private readonly GamePage _gamePage = new();

    public Grid RootGrid => Root;

    public MainWindow()
    {
        InitializeComponent();

        // Win11 材质：Mica（窗口背景跟随主题自动切换）
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

        try { AppWindow.Resize(new SizeInt32(1280, 860)); } catch { }
        try
        {
            var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "Icon.ico");
            if (File.Exists(icon)) AppWindow.SetIcon(icon);
        }
        catch { /* 图标加载失败不影响使用 */ }

        ContentHost.Children.Add(_gamePage);

        // 等对局页加载后，把标题栏注册为自定义标题栏（系统应用风格：32px 标准高度）：
        // 标题栏与页面融为一体（Mica 贯通整窗），随主题一起变色。
        _gamePage.Loaded += (_, _) =>
        {
            try
            {
                var tb = AppWindow.TitleBar;
                tb.ExtendsContentIntoTitleBar = true;
                tb.PreferredHeightOption = TitleBarHeightOption.Standard;
                SetTitleBar(_gamePage.TitleBarHost);   // Window.SetTitleBar：把标题栏设为拖拽区
            }
            catch { /* 旧系统不支持时保留系统标题栏 */ }
        };
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // 按键转发给对局页（游戏快捷键 / 光标移动）
        _gamePage.HandleKey(e);
    }
}

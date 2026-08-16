using System;
using System.IO;
using Microsoft.UI.Xaml;
using Gomoku.Services;

namespace Gomoku;

public partial class App : Application
{
    /// <summary>设置服务（键位 / 主题 / 昵称等，JSON 持久化）</summary>
    public static SettingsService Settings { get; } = new();

    /// <summary>主题服务（明暗切换、跟随系统）</summary>
    public static ThemeService Theme { get; } = new();

    public static MainWindow? MainWin { get; private set; }

    /// <summary>界面语言切换事件：订阅方刷新各自界面文本（切换立即生效）。</summary>
    public static event Action? LanguageChanged;

    public static void NotifyLanguageChanged() => LanguageChanged?.Invoke();

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Settings.Load();
        L.Apply(Settings.Language);
        MainWin = new MainWindow();
        Theme.Init(MainWin);
        MainWin.Activate();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gomoku");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{e.Exception}\n\n");
        }
        catch { /* 日志失败不影响程序 */ }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Gomoku.Services;

namespace Gomoku.Pages;

/// <summary>应用内设置小窗口：主题、昵称、语言、提示开关、键位自定义。</summary>
public sealed partial class SettingsDialog : ContentDialog
{
    private GameAction? _capturing;
    private Button? _captureButton;

    public SettingsDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => Rebuild();
        KeyDown += OnDialogKeyDown;
    }

    /// <summary>重建对话框：刷新全部本地化文本 + 当前设置值（每次打开时调用）。</summary>
    private void Rebuild()
    {
        Title = L.T("StTitle");
        CloseButtonText = L.T("StDone");
        ThemeTitle.Text = L.T("StAppearance");
        ThemeSystemText.Text = L.T("ThemeSystem");
        ThemeLightText.Text = L.T("ThemeLight");
        ThemeDarkText.Text = L.T("ThemeDark");
        ThemeHint.Text = L.T("StThemeHint");
        PrefsTitle.Text = L.T("StPrefs");
        NameTitle.Text = L.T("StName");
        HintTitle.Text = L.T("StHint");
        HintSub.Text = L.T("StHintSub");
        LangTitle.Text = L.T("StLang");
        LangSub.Text = L.T("StLangSub");
        KeysTitle.Text = L.T("StKeys");
        ResetKeysButton.Content = L.T("StReset");
        CaptureHint.Text = L.T("StCaptureHint");
        AboutTitle.Text = L.T("StAbout");
        AboutLine.Text = L.T("StAboutLine");
        AboutFeatures.Text = L.T("StFeatures");

        ThemeSelector.SelectedIndex = App.Settings.Theme switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0,
        };
        NameBox.Text = App.Settings.PlayerName;
        HintToggle.IsOn = App.Settings.HintsEnabled;
        RebuildLangList();
        RebuildKeyList();
    }

    // ---------- 主题 ----------

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        var theme = ThemeSelector.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System,
        };
        App.Theme.Apply(theme);
    }

    // ---------- 语言 ----------

    private void RebuildLangList()
    {
        LangCombo.Items.Clear();
        for (int i = 0; i < L.Languages.Length; i++)
        {
            var (code, native) = L.Languages[i];
            LangCombo.Items.Add(code == "System" ? L.T("LangSystem") : native);
            if (code == App.Settings.Language) LangCombo.SelectedIndex = i;
        }
    }

    private void OnLangChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LangCombo.SelectedIndex < 0 || LangCombo.SelectedIndex >= L.Languages.Length) return;
        var code = L.Languages[LangCombo.SelectedIndex].Code;
        if (code == App.Settings.Language) return;
        App.Settings.Language = code;
        App.Settings.Save();
        L.Apply(code);
        App.NotifyLanguageChanged();   // 刷新主页面全部文本
        Rebuild();                     // 刷新对话框自身文本（含语言选项名）
    }

    // ---------- 偏好 ----------

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        App.Settings.PlayerName = NameBox.Text.Trim();
        App.Settings.Save();
    }

    private void OnHintToggled(object sender, RoutedEventArgs e)
    {
        App.Settings.HintsEnabled = HintToggle.IsOn;
        App.Settings.Save();
    }

    // ---------- 键位 ----------

    private void RebuildKeyList()
    {
        KeyList.Children.Clear();
        foreach (var (action, _) in KeyBindings.All)
        {
            var actionName = action.ToString();
            var keys = App.Settings.KeyBindings.TryGetValue(actionName, out var list) ? list : new List<string>();
            string display = keys.Count == 0 ? L.T("StUnbound") : string.Join(" / ", keys.ConvertAll(KeyUtil.Display));

            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new TextBlock { Text = L.T($"Key{actionName}"), VerticalAlignment = VerticalAlignment.Center });
            var keyText = new TextBlock
            {
                Text = display,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.75,
                TextAlignment = TextAlignment.Right,
            };
            Grid.SetColumn(keyText, 1);
            grid.Children.Add(keyText);

            var btn = new Button { Content = L.T("StModify"), MinWidth = 64 };
            Grid.SetColumn(btn, 2);
            var capture = action;
            btn.Click += (_, _) => BeginCapture(capture, btn, keyText);
            grid.Children.Add(btn);

            var row = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                Child = grid,
            };
            KeyList.Children.Add(row);
        }
    }

    private static string LabelOf(GameAction action) => L.T($"Key{action}");

    private void BeginCapture(GameAction action, Button button, TextBlock keyText)
    {
        _capturing = action;
        _captureButton = button;
        button.Content = L.T("StCapturing");
        CaptureHint.Text = L.T("StCaptureStart", LabelOf(action));
    }

    private void OnDialogKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_capturing == null) return;
        e.Handled = true;

        if (e.Key == VirtualKey.Escape)
        {
            _capturing = null;
            if (_captureButton != null) _captureButton.Content = L.T("StModify");
            _captureButton = null;
            CaptureHint.Text = L.T("StCanceled");
            return;
        }

        var binding = KeyUtil.KeyToString(e);
        App.Settings.KeyBindings[_capturing.Value.ToString()] = new List<string> { binding };
        App.Settings.Save();
        var label = LabelOf(_capturing.Value);
        _capturing = null;
        if (_captureButton != null) _captureButton.Content = L.T("StModify");
        _captureButton = null;
        CaptureHint.Text = L.T("StBound", label, KeyUtil.Display(binding));
        RebuildKeyList();
    }

    private void OnResetKeys(object sender, RoutedEventArgs e)
    {
        App.Settings.KeyBindings = KeyBindings.Defaults();
        App.Settings.Save();
        RebuildKeyList();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Gomoku.Controls;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using Gomoku.Game;
using Gomoku.Net;
using Gomoku.Services;

namespace Gomoku.Pages;

public sealed partial class GamePage : Page
{
    private readonly GameController _c = new();
    private NetSession? _session;
    private bool _onlineConnected;
    private readonly DispatcherQueueTimer _toastTimer;
    private bool _hosting;
    private int _modeIndex;   // 0 双人 / 1 人机 / 2 联机

    public GamePage()
    {
        InitializeComponent();

        _toastTimer = DispatcherQueue.CreateTimer();
        _toastTimer.Interval = TimeSpan.FromSeconds(4);
        _toastTimer.Tick += (_, _) => { ToastBar.IsOpen = false; _toastTimer.Stop(); };

        BuildToolbarMenus();
        WireController();
        Loaded += OnLoaded;
        App.LanguageChanged += RefreshTexts;   // 语言切换即时刷新全部界面文本
    }

    /// <summary>
    /// 工具栏下拉菜单代码构建（本机 XamlCompiler 对 XAML 内嵌 MenuFlyout 有崩溃问题，与扫雷项目同理；
    /// 菜单一律代码构建）。选中项在选项左侧显示对号（其余透明占位，保持对齐）。
    /// 项目文本存资源键，语言切换时由 RefreshMenuTexts 统一刷新。
    /// </summary>
    private void BuildToolbarMenus()
    {
        ModeButton.Flyout = MakeFlyout(OnModeFlyoutClick,
            ("Mode1", "1"), ("Mode0", "0"), ("Mode2", "2"));   // 人机 / 对弈 / 联机
        DifficultyButton.Flyout = MakeFlyout(OnDifficultyFlyoutClick,
            ("Diff0", "0"), ("Diff1", "1"), ("Diff2", "2"), ("Diff3", "3"), ("Diff4", "4"));
        ColorButton.Flyout = MakeFlyout(OnColorFlyoutClick,
            ("Color0", "0"), ("Color1", "1"));
        RefreshMenuTexts();
    }

    private static MenuFlyout MakeFlyout(
        RoutedEventHandler click, params (string Key, string Tag)[] items)
    {
        var flyout = new MenuFlyout();
        foreach (var (key, tag) in items)
        {
            var item = new MenuFlyoutItem
            {
                Text = L.T(key),
                Tag = tag,
                Icon = new FontIcon { Glyph = "", FontSize = 14 },   // 对号（✓）：选中项显示，其余透明占位
            };
            item.Icon.Opacity = 0;
            item.Click += (s, _) =>
            {
                foreach (var it in flyout.Items)
                    if (it is MenuFlyoutItem m) m.Icon.Opacity = ReferenceEquals(m, s) ? 1 : 0;
                click(s, new RoutedEventArgs());
            };
            flyout.Items.Add(item);
        }
        return flyout;
    }

    private static string MenuKey(string buttonName, string tag) => buttonName switch
    {
        "ModeButton" => "Mode" + tag,
        "DifficultyButton" => "Diff" + tag,
        "ColorButton" => "Color" + tag,
        _ => tag,
    };

    private void RefreshMenuTexts()
    {
        foreach (var b in new[] { ModeButton, DifficultyButton, ColorButton })
            foreach (var item in ((MenuFlyout)b.Flyout).Items)
                if (item is MenuFlyoutItem m && m.Tag is string tag)
                    m.Text = L.T(MenuKey(b.Name, tag));
    }

    // ---------- 本地化刷新 ----------

    /// <summary>语言切换后刷新全部界面文本（立即生效，无需重启）。</summary>
    private void RefreshTexts()
    {
        RefreshToolbarTexts();
        RefreshMenuTexts();
        Board.SetKeyHint(L.T("BoardKeyHint"));
        RefreshStatus();
        UpdateOnlineUi();
    }

    private void RefreshToolbarTexts()
    {
        ModeText.Text = L.T($"ModeLabel{_modeIndex}");
        DifficultyText.Text = L.T($"Diff{_c.Difficulty}");
        ColorText.Text = L.T(_c.PlayerIsBlack ? "ColorLabelBlack" : "ColorLabelWhite");
        UndoText.Text = L.T("LblUndo");
        HintText.Text = L.T("LblHint");
        NewGameText.Text = L.T("LblNewGame");
        SettingsText.Text = L.T("LblSettings");
        OnlineText.Text = L.T("LblOnline");
        ToolTipService.SetToolTip(ModeButton, L.T("TipMode"));
        ToolTipService.SetToolTip(DifficultyButton, L.T("TipDifficulty"));
        ToolTipService.SetToolTip(ColorButton, L.T("TipColor"));
        ToolTipService.SetToolTip(UndoButton, L.T("TipUndo"));
        ToolTipService.SetToolTip(HintButton, L.T("TipHint"));
        ToolTipService.SetToolTip(NewGameButton, L.T("TipNewGame"));
        ToolTipService.SetToolTip(SettingsButton, L.T("TipSettings"));
        ToolTipService.SetToolTip(OnlineButton, L.T("TipOnline"));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _c.Difficulty = App.Settings.LastDifficulty;
        _c.PlayerIsBlack = App.Settings.LastPlayerIsBlack;
        _modeIndex = 1;   // 默认人机模式（不记忆上次模式）
        SyncModeFlyout();
        SyncDifficultyFlyout();
        SyncColorFlyout();
        _c.StartNewGame(CurrentMode());
        RefreshTexts();
    }

    private void WireController()
    {
        _c.UiDispatch = a => DispatcherQueue.TryEnqueue(() => a());
        _c.AskConfirm = (title, msg) => ConfirmAsync(title, msg);
        _c.MyName = App.Settings.PlayerName;

        _c.StateChanged += RefreshStatus;
        _c.MovePlaced += m => Board.PlaceStone(m.Col, m.Row, m.Color);
        _c.StonesRemoved += removed =>
        {
            Board.ClearMarkers();
            for (int i = 0; i < removed.Count; i++) Board.RemoveLastStone();
            var last = _c.Core.Last;
            if (last != null) Board.SetLastMarker(last.Col, last.Row);
        };
        _c.GameWon += line =>
        {
            Board.ShowWinLine(line);
            Board.GameOver = true;
            RefreshStatus();
        };
        _c.GameDrawEvent += () => RefreshStatus();
        _c.AiMoveReady += cell => DispatcherQueue.TryEnqueue(() => _c.ApplyAiMove(cell));
        _c.HintReady += cell => DispatcherQueue.TryEnqueue(() => Board.ShowHint(cell.Col, cell.Row));
        _c.Message += msg => ShowToast(msg);
        _c.GameReset += () => Board.ClearBoard();   // 开局 / 新开局：清空棋盘上的棋子与标记

        Board.CellClicked += (col, row) => _c.TryPlaceAt(col, row);
        Board.GameOver = false;
    }

    // ---------- 状态刷新 ----------

    private string PlayerLabel(StoneColor color) => _c.Mode switch
    {
        GameMode.Local2P => color == StoneColor.Black ? L.T("StBlack") : L.T("StWhite"),
        GameMode.VsAI => color == _c.AiColor
            ? L.T("StAi", DifficultyName(_c.Difficulty))
            : L.T("StPlayer", App.Settings.PlayerName),
        _ => color == _c.MyColor ? L.T("StPlayer", App.Settings.PlayerName) : L.T("StOpponent", _c.OpponentName),
    };

    private static string DifficultyName(int d) => L.T($"Diff{d}");

    private void RefreshStatus()
    {
        var c = _c;

        Board.GameOver = c.GameOver;

        // 底部状态栏：双方名字与棋子
        BlackStoneDot.Fill = BoardVisuals.StoneBrush(StoneColor.Black, ActualTheme);
        WhiteStoneDot.Fill = BoardVisuals.StoneBrush(StoneColor.White, ActualTheme);
        BlackNameText.Text = PlayerLabel(StoneColor.Black);
        WhiteNameText.Text = PlayerLabel(StoneColor.White);

        // 当前回合高亮（不在一方时双侧都不高亮）
        bool blackTurn = !c.GameOver && c.CurrentTurn == StoneColor.Black;
        HighlightName(BlackNameText, blackTurn);
        HighlightName(WhiteNameText, !c.GameOver && c.CurrentTurn == StoneColor.White);

        // 状态文案（含手数）
        string status;
        if (c.GameOver && c.Winner != null) status = L.T("StWin", PlayerLabel(c.Winner.Value));
        else if (c.GameOver && c.Draw) status = L.T("StDraw");
        else if (_c.Mode == GameMode.Online && !_onlineConnected) status = L.T("StWaitConn");
        else if (c.IsAiTurn) status = L.T("StAiThink");
        else if (c.Mode == GameMode.Online) status = L.T(_c.IsMyTurn ? "StYourTurn" : "StWaitMove");
        else status = L.T("StToMove", PlayerLabel(c.CurrentTurn));
        StatusTitle.Text = c.Core.MoveCount == 0 ? status : L.T("StTurnN", c.Core.MoveCount, status);

        // 输入与按钮可用性
        bool canAct = !c.GameOver
                      && (c.Mode != GameMode.Online || _onlineConnected)
                      && (c.Mode != GameMode.VsAI || c.CurrentTurn != c.AiColor)
                      && (c.Mode != GameMode.Online || c.IsMyTurn);
        Board.InputEnabled = canAct;
        Board.SetGhostColor(canAct ? c.CurrentTurn : null);

        UndoButton.IsEnabled = !c.GameOver && c.Core.MoveCount > 0;
        bool canHint = !c.GameOver && c.Mode != GameMode.Online
                       && (c.Mode != GameMode.VsAI || c.CurrentTurn != c.AiColor)
                       && App.Settings.HintsEnabled;
        HintButton.IsEnabled = canHint;

        // 顶栏选择器可用性
        DifficultyButton.IsEnabled = _modeIndex == 1;
        ColorButton.IsEnabled = _modeIndex == 1;

        // 联机标签：仅联机模式显示，位于设置右侧
        OnlineButton.Visibility = _modeIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

        // 联机面板状态
        UpdateOnlineUi();
    }

    private static void HighlightName(TextBlock text, bool active)
    {
        text.FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        text.Opacity = active ? 1.0 : 0.55;
    }

    // ---------- 模式 / 难度 / 颜色（顶栏 flyout） ----------

    private GameMode CurrentMode() => _modeIndex switch
    {
        1 => GameMode.VsAI,
        2 => GameMode.Online,
        _ => GameMode.Local2P,
    };

    private static void SetCheck(MenuFlyout flyout, string tag)
    {
        foreach (var item in flyout.Items)
            if (item is MenuFlyoutItem it)
                it.Icon.Opacity = it.Tag?.ToString() == tag ? 1 : 0;
    }

    private void SyncModeFlyout()
    {
        SetCheck((MenuFlyout)ModeButton.Flyout, _modeIndex.ToString());
        ModeText.Text = L.T($"ModeLabel{_modeIndex}");
    }

    private void SyncDifficultyFlyout()
    {
        SetCheck((MenuFlyout)DifficultyButton.Flyout, _c.Difficulty.ToString());
        DifficultyText.Text = L.T($"Diff{_c.Difficulty}");
    }

    private void SyncColorFlyout()
    {
        SetCheck((MenuFlyout)ColorButton.Flyout, _c.PlayerIsBlack ? "0" : "1");
        ColorText.Text = L.T(_c.PlayerIsBlack ? "ColorLabelBlack" : "ColorLabelWhite");
    }

    private void OnModeFlyoutClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item) return;
        int idx = int.Parse(item.Tag?.ToString() ?? "0");
        if (idx == _modeIndex) return;
        CloseSession();
        _modeIndex = idx;
        App.Settings.Save();
        SyncModeFlyout();
        _c.StartNewGame(CurrentMode());
        RefreshStatus();
        if (_modeIndex == 2) ShowNetworkDialog();   // 切到联机模式：自动弹出联机窗口
    }

    private void OnDifficultyFlyoutClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item) return;
        _c.Difficulty = int.Parse(item.Tag?.ToString() ?? "0");
        App.Settings.LastDifficulty = _c.Difficulty;
        App.Settings.Save();
        SyncDifficultyFlyout();
        RefreshStatus();
    }

    private void OnColorFlyoutClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item) return;
        bool black = item.Tag?.ToString() == "0";
        if (_c.PlayerIsBlack == black) return;
        _c.PlayerIsBlack = black;
        App.Settings.LastPlayerIsBlack = black;
        App.Settings.Save();
        SyncColorFlyout();
        if (_c.Mode == GameMode.VsAI) _c.StartNewGame(GameMode.VsAI);
    }

    // ---------- 顶栏按钮 ----------

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        if (_c.Mode == GameMode.Online)
        {
            if (_onlineConnected) _c.Undo();
            return;
        }
        _c.Undo();
    }

    private void OnHintClick(object sender, RoutedEventArgs e) => _c.RequestHint();

    private async void OnRestartClick(object sender, RoutedEventArgs e)
    {
        if (_c.Core.MoveCount > 0 && !_c.GameOver && _c.Mode != GameMode.Online)
        {
            if (!await ConfirmAsync(L.T("CNewGame"), L.T("CNewGameMsg"))) return;
        }
        _c.RequestNewGame();
    }

    private void OnOnlineClick(object sender, RoutedEventArgs e) => ShowNetworkDialog();

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsDialog.XamlRoot = XamlRoot;
        _ = SettingsDialog.ShowAsync();
    }

    // ---------- 提示与确认 ----------

    private bool _confirmBusy;

    /// <summary>
    /// 联机确认框（新开局 / 悔棋）。同一时刻只允许一个确认框：
    /// 对方确认框还开着时再收到请求（如连点新开局），直接返回 false 拒绝，
    /// 避免两个 ContentDialog 同时打开导致 WinUI 崩溃闪退（0xc000027b）。
    /// </summary>
    private async Task<bool> ConfirmAsync(string title, string message)
    {
        if (_confirmBusy) return false;   // 已有确认框在等：忽略重复请求
        _confirmBusy = true;
        try
        {
            var tcs = new TaskCompletionSource<bool>();
            DispatcherQueue.TryEnqueue(async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    PrimaryButtonText = L.T("Ok"),
                    CloseButtonText = L.T("Cancel"),
                    XamlRoot = XamlRoot,
                    DefaultButton = ContentDialogButton.Primary,
                };
                tcs.SetResult(await dialog.ShowAsync() == ContentDialogResult.Primary);
            });
            return await tcs.Task;
        }
        finally
        {
            _confirmBusy = false;
        }
    }

    private void ShowToast(string message)
    {
        ToastBar.Message = message;
        ToastBar.IsOpen = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    // ---------- 联机 ----------

    private void ShowNetworkDialog()
    {
        RefreshNetworkDialogTexts();
        NetworkDialog.XamlRoot = XamlRoot;
        NetHint.IsOpen = false;
        UpdateOnlineUi();
        _ = NetworkDialog.ShowAsync();
    }

    private void RefreshNetworkDialogTexts()
    {
        NetworkDialog.Title = L.T("NetTitle");
        NetworkDialog.PrimaryButtonText = L.T("NetClose");
        NetGuide.Title = L.T("NetGuideTitle");
        NetGuide.Message = L.T("NetGuideMsg");
        NetNameLabel.Text = L.T("NetName");
        NetNameBox.Text = App.Settings.PlayerName;
        HostButton.Content = L.T("NetHost");
        DiscoverButton.Content = L.T("NetDiscover");
        NetPickText.Text = L.T("NetPick");
        JoinIpBox.PlaceholderText = L.T("NetJoinIp");
        JoinButton.Content = L.T("NetJoin");
        DisconnectButton.Content = L.T("NetDisconnect");
    }

    private void OnNetNameChanged(object sender, TextChangedEventArgs e)
    {
        App.Settings.PlayerName = NetNameBox.Text.Trim();
        App.Settings.Save();
        _c.MyName = App.Settings.PlayerName;
    }

    private void CloseSession()
    {
        if (_session == null) return;
        try { _session.Dispose(); } catch { }
        _session = null;
        _onlineConnected = false;
        _hosting = false;
        _c.DetachSession();
        UpdateOnlineUi();
    }

    private void UpdateOnlineUi()
    {
        if (HostButton == null) return;   // 页面尚未初始化
        bool online = _modeIndex == 2;
        bool idle = online && !_onlineConnected && !_hosting;
        HostButton.IsEnabled = idle;
        DiscoverButton.IsEnabled = idle;
        JoinButton.IsEnabled = idle;
        JoinIpBox.IsEnabled = idle;
        HostList.IsEnabled = idle;
        DisconnectButton.Visibility = _onlineConnected || _hosting ? Visibility.Visible : Visibility.Collapsed;
        NetGuide.IsOpen = !_onlineConnected && !_hosting;

        if (!online) NetStatus.Text = L.T("NetNotOnline");
        else if (_hosting) NetStatus.Text = L.T("NetHosting");
        else if (_onlineConnected) NetStatus.Text = L.T("NetConnected", _c.OpponentName);
        else NetStatus.Text = L.T("NetIdle");
    }

    private void ShowNetError(string message)
    {
        NetHint.Message = message;
        NetHint.IsOpen = true;
    }

    private async void OnHostClick(object sender, RoutedEventArgs e)
    {
        if (_session != null || _modeIndex != 2) return;
        _hosting = true;
        NetStatus.Text = L.T("NetCreating");
        UpdateOnlineUi();

        var session = new NetSession();
        _session = session;
        HookSession(session);

        try
        {
            await session.HostAsync(App.Settings.PlayerName);
        }
        catch
        {
            _hosting = false;
            ShowNetError(L.T("NetCreateFail"));
        }
        UpdateOnlineUi();
    }

    private async void OnDiscoverClick(object sender, RoutedEventArgs e)
    {
        HostButton.IsEnabled = false;
        DiscoverButton.IsEnabled = false;
        NetStatus.Text = L.T("NetSearching");

        var hosts = await NetSession.DiscoverAsync(2500);
        HostList.ItemsSource = hosts;
        NetStatus.Text = hosts.Count == 0
            ? L.T("NetNone")
            : L.T("NetFound", hosts.Count);
        UpdateOnlineUi();
    }

    private void OnHostListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HostList.SelectedItem is NetSession.HostInfo info)
            JoinIpBox.Text = info.Ip;
    }

    private async void OnHostListDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (HostList.SelectedItem is not NetSession.HostInfo info) return;
        if (HostList.SelectionMode == ListViewSelectionMode.Single)
            JoinIpBox.Text = info.Ip;
        await JoinAsync(info.Ip);
    }

    private void OnJoinClick(object sender, RoutedEventArgs e)
    {
        var ip = JoinIpBox.Text.Trim();
        if (string.IsNullOrEmpty(ip))
        {
            ShowNetError(L.T("NetNeedIp"));
            return;
        }
        _ = JoinAsync(ip);
    }

    private async Task JoinAsync(string ip)
    {
        if (_session != null || _modeIndex != 2) return;
        NetStatus.Text = L.T("NetConnecting", ip);

        var session = new NetSession();
        _session = session;
        HookSession(session);

        try
        {
            await session.JoinAsync(ip, App.Settings.PlayerName);
        }
        catch
        {
            CloseSession();
            ShowNetError(L.T("NetFail"));
        }
        UpdateOnlineUi();
    }

    private void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        CloseSession();
        ShowToast(L.T("NetToastDisconnected"));
        RefreshStatus();
    }

    private void HookSession(NetSession session)
    {
        session.Connected += (oppName, myColor) => DispatcherQueue.TryEnqueue(() =>
        {
            _onlineConnected = true;
            _hosting = false;
            _c.AttachSession(session, App.Settings.PlayerName);
            UpdateOnlineUi();
            try { NetworkDialog.Hide(); } catch { }
            ShowToast(L.T("NetToastConnected", oppName));
        });
        session.Closed += reason => DispatcherQueue.TryEnqueue(() =>
        {
            if (_onlineConnected)
            {
                _onlineConnected = false;
                _c.DetachSession();
                ShowToast(reason);
                RefreshStatus();
            }
        });
    }

    // ---------- 键盘 ----------

    /// <summary>主窗口转发过来的按键（游戏快捷键 + 光标移动）。</summary>
    public void HandleKey(KeyRoutedEventArgs e)
    {
        var focus = FocusManager.GetFocusedElement(XamlRoot);
        bool typing = focus is TextBox or PasswordBox or AutoSuggestBox;

        // 打字 / 列表聚焦时不拦截常规按键
        bool listNav = focus is ComboBox or ListView or RadioButtons or Slider or ToggleSwitch or Button;
        var b = App.Settings.KeyBindings;

        if (typing) return;

        if (!listNav)
        {
            if (KeyUtil.Matches(b["MoveUp"], e)) { Board.MoveCursor(0, -1); e.Handled = true; return; }
            if (KeyUtil.Matches(b["MoveDown"], e)) { Board.MoveCursor(0, 1); e.Handled = true; return; }
            if (KeyUtil.Matches(b["MoveLeft"], e)) { Board.MoveCursor(-1, 0); e.Handled = true; return; }
            if (KeyUtil.Matches(b["MoveRight"], e)) { Board.MoveCursor(1, 0); e.Handled = true; return; }
            if (KeyUtil.Matches(b["PlaceStone"], e)) { Board.TryPlaceAtCursor(); e.Handled = true; return; }
        }

        if (KeyUtil.Matches(b["Undo"], e)) { _c.Undo(); e.Handled = true; }
        else if (KeyUtil.Matches(b["Hint"], e)) { _c.RequestHint(); e.Handled = true; }
        else if (KeyUtil.Matches(b["NewGame"], e)) { OnRestartClick(this, e); e.Handled = true; }
        else if (KeyUtil.Matches(b["Theme"], e)) { App.Theme.Toggle(); e.Handled = true; }
    }
}

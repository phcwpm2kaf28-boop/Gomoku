using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Gomoku.Game;

namespace Gomoku.Controls;

/// <summary>
/// 棋盘控件：纯 XAML 实时渲染。
/// 木纹圆角棋盘 + 立体棋子（径向渐变高光 + 底部暗边 + 顶部反光点），
/// 悬停幽灵棋子、键盘光标环、最后一手标记、胜局高亮连线、提示脉冲，
/// 全部使用共享静态画刷，几百个元素对核显负担极低。
/// </summary>
public sealed partial class BoardView : UserControl
{
    // 格线区域（Margin → Margin + 14*Cell = 736）在 800×800 棋盘板内左右居中（各留 64），
    // 修正"格线整体偏左、右侧空隙过大"的不对称问题。
    public new const double Margin = 64;
    public const double Cell = 48;
    public const double StoneR = 20;

    private readonly List<Ellipse> _stones = new();          // 与落子历史一一对应
    private readonly List<Ellipse> _speculars = new();
    private readonly List<StoneColor> _stoneColors = new();
    private readonly List<(int Col, int Row)> _stoneCells = new();
    private readonly HashSet<(int, int)> _occupied = new();
    private static readonly SolidColorBrush TransparentBrush = new(Microsoft.UI.Colors.Transparent);

    private Ellipse? _ghost;
    private Ellipse? _cursor;
    private Ellipse? _lastMark;
    private Ellipse? _hint;
    private Line? _winLine;
    private Line? _winGlow;
    private Storyboard? _hintSb;

    private bool _inputEnabled = true;
    private StoneColor? _ghostColor;
    private bool _gameOver;
    private int _hoverCol = -1, _hoverRow = -1;

    public event Action<int, int>? CellClicked;

    public bool InputEnabled
    {
        get => _inputEnabled;
        set
        {
            _inputEnabled = value;
            if (!value) HideGhost();
        }
    }

    /// <summary>对局是否已结束（结束时隐藏幽灵棋子）。</summary>
    public bool GameOver
    {
        get => _gameOver;
        set { _gameOver = value; UpdateGhost(); }
    }

    public int CursorCol { get; private set; } = 7;
    public int CursorRow { get; private set; } = 7;

    public BoardView()
    {
        InitializeComponent();
        BoardVisuals.Ensure(ActualTheme);
        BuildGrid();
        BuildMarkers();
        ActualThemeChanged += (_, _) => OnThemeChanged();

        // 投影需要 z 方向偏移才可见
        BoardFrame.Translation = new System.Numerics.Vector3(0, 0, 24);
    }

    // ---------- 静态绘制 ----------

    private void BuildGrid()
    {
        var lineBrush = (Brush)Application.Current.Resources["BoardLineBrush"];
        var starBrush = (Brush)Application.Current.Resources["BoardStarBrush"];

        for (int i = 0; i < 15; i++)
        {
            double pos = Margin + i * Cell;
            BoardCanvas.Children.Add(new Line
            {
                X1 = Margin, Y1 = pos, X2 = Margin + 14 * Cell, Y2 = pos,
                Stroke = lineBrush, StrokeThickness = 1.5,
            });
            BoardCanvas.Children.Add(new Line
            {
                X1 = pos, Y1 = Margin, X2 = pos, Y2 = Margin + 14 * Cell,
                Stroke = lineBrush, StrokeThickness = 1.5,
            });
        }

        // 天元 + 四星
        foreach (var (c, r) in new[] { (3, 3), (3, 11), (7, 7), (11, 3), (11, 11) })
        {
            double cx = Margin + c * Cell, cy = Margin + r * Cell;
            BoardCanvas.Children.Add(new Ellipse
            {
                Width = 10, Height = 10, Fill = starBrush,
                RenderTransform = new TranslateTransform { X = cx - 5, Y = cy - 5 },
            });
        }
    }

    private void BuildMarkers()
    {
        _ghost = NewRing(StoneR * 2, StoneR * 2, TransparentBrush, 0.0, 0);
        _ghost.Fill = TransparentBrush;
        _cursor = NewRing((StoneR + 5) * 2, (StoneR + 5) * 2, BoardVisuals.Accent, 0.0, 2.5);
        _lastMark = NewRing(26, 26, BoardVisuals.Accent, 0.0, 3);
        _hint = NewRing(46, 46, BoardVisuals.Accent, 0.0, 3.5);

        _winGlow = new Line { StrokeThickness = 14, Stroke = BoardVisuals.Accent, Opacity = 0, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false };
        _winLine = new Line { StrokeThickness = 6, Stroke = BoardVisuals.Accent, Opacity = 0, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, IsHitTestVisible = false };
        BoardCanvas.Children.Add(_winGlow);
        BoardCanvas.Children.Add(_winLine);
        BoardCanvas.Children.Add(_cursor);
        BoardCanvas.Children.Add(_lastMark);
        BoardCanvas.Children.Add(_hint);
        BoardCanvas.Children.Add(_ghost);

        Position(_cursor, CursorCol, CursorRow);
    }

    private static Ellipse NewRing(double w, double h, Brush stroke, double opacity, double thickness) => new()
    {
        Width = w, Height = h,
        Fill = TransparentBrush,
        Stroke = stroke,
        StrokeThickness = thickness,
        Opacity = opacity,
        IsHitTestVisible = false,
    };

    /// <summary>把圆环标记居中放到 (col,row) 交叉点（Left/Top 是左上角，须减半宽高）。</summary>
    private static void Position(FrameworkElement el, double col, double row)
    {
        Canvas.SetLeft(el, Margin + col * Cell - el.Width / 2);
        Canvas.SetTop(el, Margin + row * Cell - el.Height / 2);
    }

    private static (double X, double Y) Center(int col, int row) => (Margin + col * Cell, Margin + row * Cell);

    // ---------- 对外 API ----------

    public void ClearBoard()
    {
        foreach (var s in _stones) BoardCanvas.Children.Remove(s);
        foreach (var s in _speculars) BoardCanvas.Children.Remove(s);
        _stones.Clear();
        _speculars.Clear();
        _stoneColors.Clear();
        _stoneCells.Clear();
        _occupied.Clear();
        ClearMarkers();
    }

    /// <summary>清空全部叠加标记（胜局线 / 提示 / 最后一手 / 幽灵 / 光标）。</summary>
    public void ClearMarkers()
    {
        _winLine!.Opacity = 0;
        _winGlow!.Opacity = 0;
        _lastMark!.Opacity = 0;
        HideHint();
        HideGhost();
        _cursor!.Opacity = 0;
    }

    public void PlaceStone(int col, int row, StoneColor color)
    {
        var (cx, cy) = Center(col, row);

        var stone = new Ellipse
        {
            Width = StoneR * 2, Height = StoneR * 2,
            Fill = BoardVisuals.StoneBrush(color, ActualTheme),
            Stroke = BoardVisuals.StoneRim(color, ActualTheme),
            StrokeThickness = 1.2,
        };
        var spec = new Ellipse
        {
            Width = StoneR * 0.55, Height = StoneR * 0.55,
            Fill = BoardVisuals.Specular,
            Opacity = color == StoneColor.Black ? 0.30 : 0.55,
        };
        Canvas.SetLeft(stone, cx - StoneR);
        Canvas.SetTop(stone, cy - StoneR);
        Canvas.SetLeft(spec, cx - StoneR * 0.55 + StoneR * 0.42);
        Canvas.SetTop(spec, cy - StoneR * 0.55 + StoneR * 0.36);

        BoardCanvas.Children.Add(stone);
        BoardCanvas.Children.Add(spec);
        _stones.Add(stone);
        _speculars.Add(spec);
        _stoneColors.Add(color);
        _stoneCells.Add((col, row));
        _occupied.Add((col, row));
        AnimateIn(stone);

        _lastMark!.Opacity = 0.9;
        Position(_lastMark, col, row);
        HideHint();
    }

    public void RemoveLastStone()
    {
        if (_stones.Count == 0) return;
        BoardCanvas.Children.Remove(_stones[^1]);
        BoardCanvas.Children.Remove(_speculars[^1]);
        var cell = _stoneCells[^1];
        _stones.RemoveAt(_stones.Count - 1);
        _speculars.RemoveAt(_speculars.Count - 1);
        _stoneColors.RemoveAt(_stoneColors.Count - 1);
        _stoneCells.RemoveAt(_stoneCells.Count - 1);
        _occupied.Remove(cell);
    }

    public void ShowWinLine(IReadOnlyList<(int Col, int Row)> cells)
    {
        if (cells.Count < 2) return;
        var (x1, y1) = Center(cells[0].Col, cells[0].Row);
        var (x2, y2) = Center(cells[^1].Col, cells[^1].Row);
        _winLine!.X1 = x1; _winLine.Y1 = y1; _winLine.X2 = x2; _winLine.Y2 = y2;
        _winGlow!.X1 = x1; _winGlow.Y1 = y1; _winGlow.X2 = x2; _winGlow.Y2 = y2;
        _winGlow.Opacity = 0.22;

        var sb = new Storyboard();
        var a = new DoubleAnimation { From = 0, To = 0.9, Duration = TimeSpan.FromMilliseconds(350), EnableDependentAnimation = true };
        Storyboard.SetTarget(a, _winLine);
        Storyboard.SetTargetProperty(a, "Opacity");
        sb.Children.Add(a);
        sb.Begin();
    }

    public void ShowHint(int col, int row)
    {
        _hintSb?.Stop();
        _hint!.Opacity = 0.55;
        Position(_hint, col, row);
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
        var a = new DoubleAnimation { From = 0.4, To = 1.0, Duration = TimeSpan.FromMilliseconds(550), EnableDependentAnimation = true };
        Storyboard.SetTarget(a, _hint);
        Storyboard.SetTargetProperty(a, "Opacity");
        sb.Children.Add(a);
        sb.Begin();
        _hintSb = sb;
    }

    public void HideHint()
    {
        _hintSb?.Stop();
        _hintSb = null;
        _hint!.Opacity = 0;
    }

    /// <summary>在指定格显示「最后一手」小圆环。</summary>
    public void SetLastMarker(int col, int row)
    {
        _lastMark!.Opacity = 0.9;
        Position(_lastMark, col, row);
    }

    /// <summary>更新棋盘底部快捷键提示文字（本地化后由页面调用）。</summary>
    public void SetKeyHint(string text) => KeyHintText.Text = text;

    public void SetGhostColor(StoneColor? color)
    {
        _ghostColor = color;
        UpdateGhost();
    }

    public void HideGhost() => _ghost!.Opacity = 0;

    private void UpdateGhost()
    {
        bool show = _ghostColor != null && _inputEnabled && !_gameOver
                    && _hoverCol >= 0 && _hoverRow >= 0
                    && !_occupied.Contains((_hoverCol, _hoverRow));
        if (!show)
        {
            _ghost!.Opacity = 0;
            return;
        }
        _ghost!.Fill = BoardVisuals.StoneBrush(_ghostColor!.Value, ActualTheme);
        _ghost.Stroke = BoardVisuals.StoneRim(_ghostColor.Value, ActualTheme);
        _ghost.Opacity = 0.4;
        Position(_ghost, _hoverCol, _hoverRow);
    }

    // ---------- 键盘光标 ----------

    public void MoveCursor(int dc, int dr)
    {
        CursorCol = Math.Clamp(CursorCol + dc, 0, 14);
        CursorRow = Math.Clamp(CursorRow + dr, 0, 14);
        _cursor!.Opacity = 0.9;
        Position(_cursor, CursorCol, CursorRow);
        HideGhost();
    }

    public void TryPlaceAtCursor()
    {
        if (_inputEnabled && !_gameOver) CellClicked?.Invoke(CursorCol, CursorRow);
    }

    // ---------- 指针交互（鼠标 / 触摸 / 触控笔） ----------

    private (int Col, int Row) CellFromPoint(Point p)
    {
        int col = (int)Math.Round((p.X - Margin) / Cell);
        int row = (int)Math.Round((p.Y - Margin) / Cell);
        if (col < 0 || col >= 15 || row < 0 || row >= 15) return (-1, -1);
        double dx = p.X - (Margin + col * Cell);
        double dy = p.Y - (Margin + row * Cell);
        if (Math.Sqrt(dx * dx + dy * dy) > Cell * 0.6) return (-1, -1);   // 距交叉点太远视为无效
        return (col, row);
    }

    private void OnCanvasPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var (c, r) = CellFromPoint(e.GetCurrentPoint(BoardCanvas).Position);
        if (c == _hoverCol && r == _hoverRow) return;
        _hoverCol = c;
        _hoverRow = r;
        _cursor!.Opacity = 0;   // 鼠标模式下隐藏键盘光标
        UpdateGhost();
    }

    private void OnCanvasPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var (c, r) = CellFromPoint(e.GetCurrentPoint(BoardCanvas).Position);
        if (c < 0 || r < 0) return;
        if (_inputEnabled && !_gameOver) CellClicked?.Invoke(c, r);
    }

    private void OnCanvasPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _hoverCol = _hoverRow = -1;
        HideGhost();
    }

    // ---------- 动画与主题 ----------

    private void AnimateIn(Ellipse stone)
    {
        var st = new ScaleTransform { CenterX = StoneR, CenterY = StoneR, ScaleX = 0.35, ScaleY = 0.35 };
        stone.RenderTransform = st;
        var sb = new Storyboard();
        foreach (var prop in new[] { "ScaleX", "ScaleY" })
        {
            var a = new DoubleAnimation { From = 0.35, To = 1.0, Duration = TimeSpan.FromMilliseconds(150), EnableDependentAnimation = true };
            Storyboard.SetTarget(a, st);
            Storyboard.SetTargetProperty(a, prop);
            sb.Children.Add(a);
        }
        sb.Begin();
    }

    public void OnThemeChanged()
    {
        BoardVisuals.Ensure(ActualTheme);
        for (int i = 0; i < _stones.Count; i++)
        {
            var color = _stoneColors[i];
            _stones[i].Fill = BoardVisuals.StoneBrush(color, ActualTheme);
            _stones[i].Stroke = BoardVisuals.StoneRim(color, ActualTheme);
        }
        _cursor!.Stroke = BoardVisuals.Accent;
        _lastMark!.Stroke = BoardVisuals.Accent;
        _hint!.Stroke = BoardVisuals.Accent;
        _winLine!.Stroke = BoardVisuals.Accent;
        _winGlow!.Stroke = BoardVisuals.Accent;
        UpdateGhost();
    }
}

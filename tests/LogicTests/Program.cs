using System.Diagnostics;
using Gomoku.AI;
using Gomoku.Game;

// ── 微型测试框架 ──
int passed = 0, failed = 0;
void Check(string name, bool ok)
{
    if (ok) { passed++; Console.WriteLine($"  PASS  {name}"); }
    else { failed++; Console.WriteLine($"  FAIL  {name}"); }
}

static byte[,] Empty() => new byte[15, 15];

static void Put(byte[,] b, int c, int r, StoneColor col) => b[c, r] = (byte)col;

Console.WriteLine("== GameCore 测试 ==");

// 横向五连
{
    var core = new GameCore();
    for (int c = 3; c <= 7; c++) core.TryPlace(c, 7, StoneColor.Black, out _);
    Check("横向五连判定", core.CheckWinAtLast(out var line) && line.Count == 5);
    Check("连线方向正确", line[0].Col == 3 && line[^1].Col == 7);
}
// 纵向 / 斜向 / 反斜向
{
    var core = new GameCore();
    for (int r = 2; r <= 6; r++) core.TryPlace(5, r, StoneColor.White, out _);
    Check("纵向五连判定", core.CheckWinAtLast(out _));
    core = new GameCore();
    for (int i = 0; i < 5; i++) core.TryPlace(3 + i, 3 + i, StoneColor.Black, out _);
    Check("斜向五连判定", core.CheckWinAtLast(out _));
    core = new GameCore();
    for (int i = 0; i < 5; i++) core.TryPlace(10 - i, 4 + i, StoneColor.Black, out _);
    Check("反斜向五连判定", core.CheckWinAtLast(out _));
}
// 四连不算赢
{
    var core = new GameCore();
    for (int c = 3; c <= 6; c++) core.TryPlace(c, 7, StoneColor.Black, out _);
    Check("四连不判胜", !core.CheckWinAtLast(out _));
}
// 六连也赢（连子超五）
{
    var core = new GameCore();
    for (int c = 3; c <= 8; c++) core.TryPlace(c, 7, StoneColor.Black, out _);
    Check("六连判胜", core.CheckWinAtLast(out var l6) && l6.Count >= 5);
}
// 交替落子顺序
{
    var core = new GameCore();
    Check("黑先", core.NextToMove == StoneColor.Black);
    core.TryPlace(7, 7, StoneColor.Black, out _);
    Check("黑白交替", core.NextToMove == StoneColor.White);
}
// 悔棋
{
    var core = new GameCore();
    core.TryPlace(7, 7, StoneColor.Black, out _);
    core.TryPlace(7, 8, StoneColor.White, out _);
    var removed = core.UndoRound();
    Check("悔棋撤两步", removed.Count == 2 && core.MoveCount == 0);
    Check("悔棋后轮到黑方", core.NextToMove == StoneColor.Black);
}
// 占用格不可落子
{
    var core = new GameCore();
    core.TryPlace(7, 7, StoneColor.Black, out _);
    Check("占用格拒绝落子", !core.TryPlace(7, 7, StoneColor.White, out _));
    Check("越界拒绝落子", !core.TryPlace(-1, 0, StoneColor.Black, out _) && !core.TryPlace(15, 15, StoneColor.Black, out _));
}

Console.WriteLine("== AiEngine 测试 ==");

// 空盘第一手
{
    var b = Empty();
    var m = AiEngine.FindBestMove(b, StoneColor.Black, 2, CancellationToken.None);
    Check("空盘落天元", m == new Cell(7, 7));
}
// 一步成五必取
{
    var b = Empty();
    for (int c = 3; c <= 6; c++) Put(b, c, 7, StoneColor.Black);
    var m = AiEngine.FindBestMove(b, StoneColor.Black, 1, CancellationToken.None);
    Check("成五点必取", m == new Cell(7, 7) || m == new Cell(2, 7));
}
// 对方活四必须堵
{
    var b = Empty();
    for (int c = 3; c <= 6; c++) Put(b, c, 7, StoneColor.Black);
    Put(b, 0, 0, StoneColor.White);
    Put(b, 7, 7, StoneColor.White);
    var m = AiEngine.FindBestMove(b, StoneColor.White, 1, CancellationToken.None);
    Check("堵活四", m == new Cell(7, 7) || m == new Cell(2, 7));
}
// 各难度不崩溃且速度可接受
{
    var b = Empty();
    Put(b, 7, 7, StoneColor.Black);
    Put(b, 7, 8, StoneColor.White);
    Put(b, 8, 8, StoneColor.Black);
    Put(b, 6, 6, StoneColor.White);
    var sw = Stopwatch.StartNew();
    var m0 = AiEngine.FindBestMove(b, StoneColor.Black, 0, CancellationToken.None);
    Check($"简单难度正常（{sw.ElapsedMilliseconds}ms）", m0.Col >= 0 && m0.Col < 15);

    var sw4 = Stopwatch.StartNew();
    var m4 = AiEngine.FindBestMove(b, StoneColor.Black, 4, CancellationToken.None);
    sw4.Stop();
    Check($"大师难度 < 4s（{sw4.ElapsedMilliseconds}ms）", sw4.ElapsedMilliseconds < 4000 && m4.Col >= 0);
}
// 大师必须堵住一步成五（深度足够）
{
    var b = Empty();
    for (int c = 3; c <= 6; c++) Put(b, c, 7, StoneColor.Black);   // 黑方活四
    Put(b, 0, 0, StoneColor.White);
    Put(b, 7, 7, StoneColor.White);
    var sw = Stopwatch.StartNew();
    var m = AiEngine.FindBestMove(b, StoneColor.White, 4, CancellationToken.None);
    sw.Stop();
    Check($"大师堵活四（{sw.ElapsedMilliseconds}ms）", m == new Cell(7, 7) || m == new Cell(2, 7));
}
// 大师优先成五（对方活四 + 自己活四时，自己成五）
{
    var b = Empty();
    for (int c = 3; c <= 6; c++) Put(b, c, 7, StoneColor.Black);   // 对方活四
    for (int r = 3; r <= 6; r++) Put(b, 2, r, StoneColor.White);   // 自己活四 → 成五(2,7) 或 (2,2)
    Put(b, 0, 0, StoneColor.Black);
    var m = AiEngine.FindBestMove(b, StoneColor.White, 4, CancellationToken.None);
    bool win = m == new Cell(2, 7) || m == new Cell(2, 2);
    Check("优先成五而非单纯防守", win);
}

Console.WriteLine();
Console.WriteLine($"通过 {passed} 项，失败 {failed} 项");
return failed == 0 ? 0 : 1;

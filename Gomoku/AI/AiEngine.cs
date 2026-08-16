using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Gomoku.Game;

namespace Gomoku.AI;

/// <summary>
/// 五子棋 AI 引擎。
/// 评分：把横 / 竖 / 两条对角线展开成带边界墙的线，用 5/6 格滑动窗口查预编译棋形表；
/// 搜索：负极大值 + alpha-beta 剪枝，候选点按“落子收益”（进攻 + 防守）排序并截断，
/// 大师难度附加迭代加深 + 时间预算。
/// </summary>
public static class AiEngine
{
    // 棋形权重
    private const int W_FIVE = 1_000_000;
    private const int W_LIVE4 = 100_000;
    private const int W_RUSH4 = 25_000;
    private const int W_LIVE3 = 20_000;
    private const int W_SLEEP3 = 1_500;
    private const int W_LIVE2 = 500;
    private const int W_SLEEP2 = 150;
    private const int W_ONE_OPEN = 40;
    private const int W_ONE_BLOCK = 10;

    private const int INF = int.MaxValue / 4;

    private const int Size = GameCore.Size;

    // 5/6 格窗口棋形表：每格 2bit（0 空 / 1 我 / 2 对方），直接索引查表
    private static readonly int[] T5 = new int[1 << 10];
    private static readonly int[] T6 = new int[1 << 12];

    private static readonly int[][] DirPairs =
    {
        new[] { 1, 0 }, new[] { 0, 1 }, new[] { 1, 1 }, new[] { 1, -1 },
    };

    static AiEngine() => BuildTables();

    private static int EncodeChar(char ch) => ch switch { '0' => 0, '1' => 1, _ => 2 };

    private static void BuildTables()
    {
        void Add(string p, int w)
        {
            var r = new string(p.Reverse().ToArray());
            foreach (var q in new[] { p, r })
            {
                int code = 0;
                foreach (var c in q) code = (code << 2) | EncodeChar(c);
                if (q.Length == 5) T5[code] += w; else T6[code] += w;
            }
        }

        Add("11111", W_FIVE);
        Add("01111", W_RUSH4);
        Add("10111", W_RUSH4);
        Add("11011", W_RUSH4);
        Add("011110", W_LIVE4);
        Add("211110", W_RUSH4);
        Add("211112", 800);                    // 死四（两端被堵）
        Add("01110", W_LIVE3);
        Add("010110", W_LIVE3);                // 跳活三
        Add("011010", W_LIVE3);
        Add("110110", W_RUSH4);                // 跳冲四
        Add("101110", W_RUSH4);
        Add("010111", W_RUSH4);
        Add("21110", W_SLEEP3);
        Add("21112", W_SLEEP3);
        Add("11100", W_SLEEP3);
        Add("01101", W_SLEEP3);
        Add("10110", W_SLEEP3);
        Add("11010", W_SLEEP3);
        Add("01011", W_SLEEP3);
        Add("21101", W_SLEEP3);
        Add("10112", W_SLEEP3);
        Add("00110", W_LIVE2);
        Add("01010", W_LIVE2);
        Add("10100", W_LIVE2);
        Add("00101", W_LIVE2);
        Add("10010", W_LIVE2);
        Add("01001", W_LIVE2);
        Add("21100", 400);
        Add("21010", 300);
        Add("20101", 300);
        Add("00100", W_ONE_OPEN);
        Add("01000", W_ONE_OPEN);
        Add("10000", W_ONE_BLOCK);
        Add("20001", 5);
    }

    /// <summary>计算一条线（line[0]=左墙，line[1..n]=棋子，line[n+1]=右墙）上某色的棋形总分。</summary>
    private static int EvalLine(int[] line, int n)
    {
        int score = 0;
        for (int s = 1; s + 4 <= n + 1; s++)
        {
            int code = 0;
            for (int i = s; i < s + 5; i++) code = (code << 2) | line[i];
            score += T5[code];
        }
        for (int s = 1; s + 5 <= n + 1; s++)
        {
            int code = 0;
            for (int i = s; i < s + 6; i++) code = (code << 2) | line[i];
            score += T6[code];
        }
        return score;
    }

    private static int Map(byte v, byte me) => v == 0 ? 0 : (v == me ? 1 : 2);

    /// <summary>整盘评估：me 色（视为 1）相对对方的总分。</summary>
    private static int EvalFor(byte[,] board, byte me)
    {
        int total = 0;
        var line = new int[Size + 2];
        line[0] = 2;
        line[Size + 1] = 2;

        // 行
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++) line[c + 1] = Map(board[c, r], me);
            total += EvalLine(line, Size);
        }
        // 列
        for (int c = 0; c < Size; c++)
        {
            for (int r = 0; r < Size; r++) line[r + 1] = Map(board[c, r], me);
            total += EvalLine(line, Size);
        }
        // 主对角线（c - r = k）
        for (int k = 1 - Size; k <= Size - 1; k++)
        {
            int n = 0;
            for (int r = 0; r < Size; r++)
            {
                int c = r + k;
                if (c >= 0 && c < Size) { line[n + 1] = Map(board[c, r], me); n++; }
            }
            if (n >= 5) total += EvalLine(line, n);
        }
        // 反对角线（c + r = k）
        for (int k = 0; k <= 2 * Size - 2; k++)
        {
            int n = 0;
            for (int r = 0; r < Size; r++)
            {
                int c = k - r;
                if (c >= 0 && c < Size) { line[n + 1] = Map(board[c, r], me); n++; }
            }
            if (n >= 5) total += EvalLine(line, n);
        }
        return total;
    }

    /// <summary>落子 (c,r) 后，me 色 4 条过线棋形分值的增量（进攻值）。</summary>
    private static int LineDelta(byte[,] board, int c, int r, byte me)
    {
        int delta = 0;
        var line = new int[Size + 2];
        foreach (var d in DirPairs)
        {
            int dc = d[0], dr = d[1];
            int n = 0, idx = 0;
            for (int t = -Size; t <= Size; t++)
            {
                int cc = c + dc * t, rr = r + dr * t;
                if (cc < 0 || cc >= Size || rr < 0 || rr >= Size) continue;
                if (t == 0) idx = n + 1;
                line[n + 1] = Map(board[cc, rr], me);
                n++;
            }
            if (n < 5) continue;
            line[0] = 2;
            line[n + 1] = 2;
            int before = EvalLine(line, n);
            line[idx] = 1;                                  // 模拟落子
            int after = EvalLine(line, n);
            delta += after - before;
        }
        return delta;
    }

    /// <summary>落子收益 = 己方进攻增量 + 对方进攻增量（即堵截价值）。</summary>
    private static int CellScore(byte[,] board, int c, int r, byte me)
        => LineDelta(board, c, r, me) + LineDelta(board, c, r, (byte)(me == 1 ? 2 : 1));

    /// <summary>候选点：已有棋子曼哈顿邻域（Chebyshev ≤ 2）内的空位，按收益排序截断。</summary>
    private static List<(Cell Pos, int Score)> CandidateMoves(byte[,] board, byte me, int cap, CancellationToken ct)
    {
        var near = new bool[Size, Size];
        bool any = false;
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
            {
                if (board[c, r] == 0) continue;
                any = true;
                for (int dr = -2; dr <= 2; dr++)
                    for (int dc = -2; dc <= 2; dc++)
                    {
                        int cc = c + dc, rr = r + dr;
                        if (cc >= 0 && cc < Size && rr >= 0 && rr < Size) near[cc, rr] = true;
                    }
            }

        var list = new List<(Cell, int)>();
        if (!any) { list.Add((new Cell(7, 7), 0)); return list; }

        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
            {
                if (!near[c, r] || board[c, r] != 0) continue;
                int s = CellScore(board, c, r, me);
                s += 2 - Math.Max(Math.Abs(c - 7), Math.Abs(r - 7)) / 6;   // 轻微中心偏好
                list.Add((new Cell(c, r), s));
            }

        list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        if (list.Count > cap) list.RemoveRange(cap, list.Count - cap);
        return list;
    }

    private static int DepthCap(int depth, int rootCap) => depth switch
    {
        >= 5 => 5,
        4 => 8,
        3 => 10,
        _ => rootCap,
    };

    private static int EvaluateBoard(byte[,] board, byte side)
    {
        byte op = side == (byte)1 ? (byte)2 : (byte)1;
        return EvalFor(board, side) - EvalFor(board, op);
    }

    private static int Negamax(byte[,] board, byte side, int depth, int alpha, int beta,
        CancellationToken ct, Stopwatch sw, int budgetMs, int rootCap)
    {
        if (ct.IsCancellationRequested || (budgetMs > 0 && sw.ElapsedMilliseconds >= budgetMs)) return 0;
        if (depth <= 0) return EvaluateBoard(board, side);

        var moves = CandidateMoves(board, side, DepthCap(depth, rootCap), ct);
        if (moves.Count == 0) return 0;

        int best = -INF;
        foreach (var (pos, _) in moves)
        {
            board[pos.Col, pos.Row] = side;
            int v = -Negamax(board, side == (byte)1 ? (byte)2 : (byte)1, depth - 1, -beta, -alpha, ct, sw, budgetMs, rootCap);
            board[pos.Col, pos.Row] = 0;
            if (v > best) best = v;
            if (best > alpha) alpha = best;
            if (alpha >= beta) break;
        }
        return best;
    }

    /// <summary>迭代加深：整轮搜索完成才更新 best，超时保留上一轮结果。</summary>
    private static bool SearchRoot(byte[,] board, byte my, byte op, int depth, int cap,
        CancellationToken ct, Stopwatch sw, int budget, ref Cell best, ref int bestScore)
    {
        var moves = CandidateMoves(board, my, cap, ct);
        int alpha = -INF, sc = -INF;
        Cell b = best;
        foreach (var (pos, _) in moves)
        {
            board[pos.Col, pos.Row] = my;
            int v = -Negamax(board, op, depth - 1, -INF, -alpha, ct, sw, budget, cap);
            board[pos.Col, pos.Row] = 0;
            if (v > sc) { sc = v; b = pos; }
            if (sc > alpha) alpha = sc;
            if (sw.ElapsedMilliseconds >= budget || ct.IsCancellationRequested) return false;
        }
        best = b;
        bestScore = sc;
        return true;
    }

    /// <summary>
    /// 计算当前棋盘下 me 的最佳落点。
    /// difficulty：0 简单 / 1 普通 / 2 困难 / 3 专家 / 4 大师
    /// </summary>
    public static Cell FindBestMove(byte[,] board, StoneColor me, int difficulty, CancellationToken ct)
    {
        byte my = (byte)me, op = (byte)GameCore.Opposite(me);
        int depth = difficulty switch { 0 => 1, 1 => 2, 2 => 3, 3 => 4, _ => 4 };
        int rootCap = difficulty switch { 0 => 8, 1 => 10, 2 => 14, 3 => 18, _ => 20 };

        var moves = CandidateMoves(board, my, 48, ct);
        if (moves.Count == 0) return new Cell(7, 7);

        // 1) 直接取胜
        foreach (var (pos, _) in moves)
        {
            board[pos.Col, pos.Row] = my;
            bool win = GameCore.HasFive(board, pos.Col, pos.Row);
            board[pos.Col, pos.Row] = 0;
            if (win) return pos;
        }

        // 2) 必须堵截对方一步成五
        Cell? block = null;
        int blockScore = int.MinValue;
        foreach (var (pos, _) in moves)
        {
            board[pos.Col, pos.Row] = op;
            bool threat = GameCore.HasFive(board, pos.Col, pos.Row);
            board[pos.Col, pos.Row] = 0;
            if (threat)
            {
                int s = CellScore(board, pos.Col, pos.Row, my);
                if (s > blockScore) { blockScore = s; block = pos; }
            }
        }
        if (block != null) return block.Value;

        // 3) 简单难度：浅层打分 + 随机扰动
        if (difficulty == 0)
        {
            var top = moves.Take(3).ToList();
            return Random.Shared.Next(10) < 3 ? top[Random.Shared.Next(top.Count)].Pos : top[0].Pos;
        }

        var sw = Stopwatch.StartNew();
        int budget = difficulty == 4 ? 2500 : 0;
        var scored = moves.Count > rootCap ? moves.Take(rootCap).ToList() : moves;

        int bestScore = -INF;
        Cell best = scored[0].Pos;
        int alpha = -INF;
        foreach (var (pos, _) in scored)
        {
            board[pos.Col, pos.Row] = my;
            int v = -Negamax(board, op, depth - 1, -INF, -alpha, ct, sw, budget, rootCap);
            board[pos.Col, pos.Row] = 0;
            if (v > bestScore) { bestScore = v; best = pos; }
            if (bestScore > alpha) alpha = bestScore;
            if (budget > 0 && sw.ElapsedMilliseconds > budget * 3 / 4) break;   // 给迭代加深留时间
        }

        // 4) 大师：迭代加深，深度 5~6
        if (difficulty == 4)
        {
            for (int d = 5; d <= 6 && sw.ElapsedMilliseconds < budget && !ct.IsCancellationRequested; d++)
            {
                if (!SearchRoot(board, my, op, d, rootCap, ct, sw, budget, ref best, ref bestScore)) break;
            }
        }
        return best;
    }
}

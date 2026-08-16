using System;
using System.Collections.Generic;
using System.Linq;

namespace Gomoku.Game;

/// <summary>棋子颜色</summary>
public enum StoneColor : byte { Black = 1, White = 2 }

/// <summary>棋盘格坐标（列, 行），0 起点</summary>
public readonly record struct Cell(int Col, int Row);

/// <summary>
/// 纯逻辑核心：棋盘状态、落子、悔棋、胜负判定。
/// 不依赖任何 UI 类型，可独立单元测试。
/// </summary>
public class GameCore
{
    public const int Size = 15;

    private readonly byte[,] _board = new byte[Size, Size];
    private readonly List<MoveRecord> _history = new();

    public IReadOnlyList<MoveRecord> History => _history;
    public int MoveCount => _history.Count;
    public bool IsFull => _history.Count == Size * Size;
    public MoveRecord? Last => _history.Count > 0 ? _history[^1] : null;

    public record MoveRecord(int Col, int Row, StoneColor Color, int Number);

    public byte this[int col, int row] => _board[col, row];
    public bool IsEmpty(int col, int row) => _board[col, row] == 0;

    public StoneColor NextToMove => _history.Count == 0 ? StoneColor.Black : Opposite(Last!.Color);

    public static StoneColor Opposite(StoneColor c) => c == StoneColor.Black ? StoneColor.White : StoneColor.Black;

    public void Reset()
    {
        Array.Clear(_board);
        _history.Clear();
    }

    public bool TryPlace(int col, int row, StoneColor color, out MoveRecord record)
    {
        record = default!;
        if (col < 0 || col >= Size || row < 0 || row >= Size || _board[col, row] != 0) return false;
        _board[col, row] = (byte)color;
        record = new MoveRecord(col, row, color, _history.Count + 1);
        _history.Add(record);
        return true;
    }

    public MoveRecord? Pop()
    {
        if (_history.Count == 0) return null;
        var rec = _history[^1];
        _board[rec.Col, rec.Row] = 0;
        _history.RemoveAt(_history.Count - 1);
        return rec;
    }

    /// <summary>悔一回合：撤掉最后两步（双人 / 人机通用）。</summary>
    public List<MoveRecord> UndoRound()
    {
        var removed = new List<MoveRecord>();
        var a = Pop();
        if (a != null) removed.Add(a);
        var b = Pop();
        if (b != null) removed.Add(b);
        return removed;
    }

    /// <summary>落子 (col,row) 后该色是否已连成五子及以上。</summary>
    public static bool HasFive(byte[,] board, int col, int row)
    {
        byte color = board[col, row];
        if (color == 0) return false;
        int[] dc = { 1, 0, 1, 1 };
        int[] dr = { 0, 1, 1, -1 };
        for (int d = 0; d < 4; d++)
        {
            int count = 1;
            for (int s = -1; s <= 1; s += 2)
            {
                int c = col + dc[d] * s, r = row + dr[d] * s;
                while (c >= 0 && c < Size && r >= 0 && r < Size && board[c, r] == color)
                {
                    count++;
                    c += dc[d] * s;
                    r += dr[d] * s;
                }
            }
            if (count >= 5) return true;
        }
        return false;
    }

    /// <summary>若最后一步获胜，返回按方向排序的获胜连线（用于绘制高亮）；否则返回 false。</summary>
    public bool CheckWinAtLast(out List<(int Col, int Row)> line)
    {
        line = new List<(int, int)>();
        var last = Last;
        if (last == null) return false;
        byte color = (byte)last.Color;
        int[] dc = { 1, 0, 1, 1 };
        int[] dr = { 0, 1, 1, -1 };
        for (int d = 0; d < 4; d++)
        {
            var cells = new List<(int Col, int Row)> { (last.Col, last.Row) };
            for (int s = -1; s <= 1; s += 2)
            {
                int c = last.Col + dc[d] * s, r = last.Row + dr[d] * s;
                while (c >= 0 && c < Size && r >= 0 && r < Size && _board[c, r] == color)
                {
                    cells.Add((c, r));
                    c += dc[d] * s;
                    r += dr[d] * s;
                }
            }
            if (cells.Count >= 5)
            {
                int dd = dc[d], rd = dr[d];
                line = cells.OrderBy(p => p.Col * dd + p.Row * rd).ToList();
                return true;
            }
        }
        return false;
    }

    public byte[,] Snapshot()
    {
        var copy = new byte[Size, Size];
        Buffer.BlockCopy(_board, 0, copy, 0, _board.Length);
        return copy;
    }
}

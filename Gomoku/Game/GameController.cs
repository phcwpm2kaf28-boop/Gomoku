using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gomoku.AI;
using Gomoku.Game;
using Gomoku.Net;
using Gomoku.Services;

namespace Gomoku.Game;

public enum GameMode { Local2P, VsAI, Online }

/// <summary>
/// 对局控制器：把 双人 / 人机 / 联机 三种模式统一成一套状态机。
/// 负责回合流转、悔棋、提示、AI 异步思考、联机消息协调。
/// </summary>
public class GameController
{
    public GameCore Core { get; } = new();

    public GameMode Mode { get; private set; } = GameMode.Local2P;
    public int Difficulty { get; set; } = 2;
    public bool PlayerIsBlack { get; set; } = true;
    public bool GameOver { get; private set; }
    public StoneColor? Winner { get; private set; }
    public bool Draw { get; private set; }
    public bool OnlineConnected { get; private set; }

    public NetSession? Session { get; private set; }
    public string OpponentName { get; private set; } = "";
    public StoneColor MyColor { get; private set; } = StoneColor.Black;
    public string MyName { get; set; } = "玩家";

    public StoneColor CurrentTurn => Core.NextToMove;
    public StoneColor? AiColor => Mode == GameMode.VsAI ? (PlayerIsBlack ? StoneColor.White : StoneColor.Black) : null;
    public bool IsAiTurn => Mode == GameMode.VsAI && !GameOver && CurrentTurn == AiColor;
    public bool IsMyTurn => !GameOver && (Mode != GameMode.Online || CurrentTurn == MyColor);

    // ---- 事件（全部在 UI 线程触发） ----
    public event Action? StateChanged;
    public event Action<GameCore.MoveRecord>? MovePlaced;
    public event Action<List<GameCore.MoveRecord>>? StonesRemoved;
    public event Action<List<(int Col, int Row)>>? GameWon;
    public event Action? GameDrawEvent;
    public event Action<Cell>? AiMoveReady;
    public event Action<Cell>? HintReady;
    public event Action<string>? Message;
    /// <summary>棋盘已重置（开局 / 新开局 / 联机重开），页面应清空棋盘视觉。</summary>
    public event Action? GameReset;
    /// <summary>联机确认对话框（由页面注入），在接收线程调用，需自行调度。</summary>
    public Func<string, string, Task<bool>>? AskConfirm;

    /// <summary>UI 线程调度器（由页面注入）。</summary>
    public Action<Action>? UiDispatch;

    private CancellationTokenSource? _aiCts;
    private CancellationTokenSource? _hintCts;

    // ---------- 开局 ----------

    public void StartNewGame(GameMode mode)
    {
        CancelThinking();
        Mode = mode;
        GameOver = false;
        Winner = null;
        Draw = false;
        Core.Reset();
        GameReset?.Invoke();
        RaiseStateChanged();
        if (mode == GameMode.VsAI && !PlayerIsBlack) ScheduleAiMove();   // 玩家执白，电脑先手
    }

    /// <summary>联机模式：等连接建立后由 AttachSession 的连接事件调用。</summary>
    private void PrepareOnline()
    {
        CancelThinking();
        Mode = GameMode.Online;
        GameOver = false;
        Winner = null;
        Draw = false;
        OnlineConnected = true;
        Core.Reset();
        GameReset?.Invoke();
        RaiseStateChanged();
    }

    // ---------- 落子 ----------

    /// <summary>当前玩家（人类）落子入口。</summary>
    public bool TryPlaceAt(int col, int row)
    {
        if (GameOver) return false;
        if (Mode == GameMode.Online && !OnlineConnected) return false;
        if (Mode == GameMode.VsAI && CurrentTurn == AiColor) return false;   // 电脑思考中
        if (!IsMyTurn) return false;
        if (!Core.IsEmpty(col, row)) return false;

        DoPlace(col, row);
        return true;
    }

    private void DoPlace(int col, int row)
    {
        var color = Core.NextToMove;
        if (!Core.TryPlace(col, row, color, out var rec)) return;
        MovePlaced?.Invoke(rec);

        if (Core.CheckWinAtLast(out var line))
        {
            GameOver = true;
            Winner = color;
            GameWon?.Invoke(line);
        }
        else if (Core.IsFull)
        {
            GameOver = true;
            Draw = true;
            GameDrawEvent?.Invoke();
        }
        else if (Mode == GameMode.VsAI && CurrentTurn == AiColor)
        {
            ScheduleAiMove();
        }
        RaiseStateChanged();
    }

    /// <summary>AI 思考完成后的落子（由页面调度回 UI 线程调用）。</summary>
    public void ApplyAiMove(Cell cell)
    {
        if (Mode != GameMode.VsAI || GameOver) return;
        if (Core.IsEmpty(cell.Col, cell.Row)) DoPlace(cell.Col, cell.Row);
    }

    /// <summary>人机对局中电脑落子的模拟思考延迟：高难度稍长，制造真实对弈感。</summary>
    private static readonly TimeSpan AiThinkPause = TimeSpan.FromMilliseconds(400);

    private void ScheduleAiMove()
    {
        _aiCts?.Cancel();
        _aiCts = new CancellationTokenSource();
        var ct = _aiCts.Token;
        var color = AiColor!.Value;
        var snapshot = Core.Snapshot();
        int difficulty = Difficulty;

        // 思考完成后稍微停顿再落子（悔棋 / 新开局会取消），对弈更有真实感
        var pause = AiThinkPause + TimeSpan.FromMilliseconds(difficulty * 100);
        Task.Run(() => AiEngine.FindBestMove(snapshot, color, difficulty, ct))
            .ContinueWith(async t =>
            {
                if (!t.IsCompletedSuccessfully || ct.IsCancellationRequested) return;
                try { await Task.Delay(pause, ct); } catch (TaskCanceledException) { return; }
                if (!ct.IsCancellationRequested) AiMoveReady?.Invoke(t.Result);
            });
    }

    // ---------- 悔棋 ----------

    public void Undo()
    {
        if (GameOver || Core.MoveCount == 0) return;
        if (Mode == GameMode.Online)
        {
            Session?.RequestUndo();
            return;
        }
        CancelThinking();
        var removed = Core.UndoRound();
        if (removed.Count > 0)
        {
            StonesRemoved?.Invoke(removed);
            RaiseStateChanged();
        }
    }

    public void ApplyNetUndo()
    {
        CancelThinking();
        var removed = Core.UndoRound();
        if (removed.Count > 0)
        {
            StonesRemoved?.Invoke(removed);
            RaiseStateChanged();
        }
    }

    // ---------- 提示 ----------

    public void RequestHint()
    {
        if (GameOver || Mode == GameMode.Online) return;
        if (Mode == GameMode.VsAI && CurrentTurn == AiColor) return;
        if (!App.Settings.HintsEnabled) return;

        _hintCts?.Cancel();
        _hintCts = new CancellationTokenSource();
        var ct = _hintCts.Token;
        var color = CurrentTurn;
        var snapshot = Core.Snapshot();
        int depth = Mode == GameMode.VsAI ? Difficulty : 3;

        Task.Run(() => AiEngine.FindBestMove(snapshot, color, depth, ct))
            .ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && !ct.IsCancellationRequested)
                    HintReady?.Invoke(t.Result);
            });
    }

    // ---------- 联机 ----------

    public void AttachSession(NetSession session, string myName)
    {
        DetachSession();
        Session = session;
        MyName = myName;

        session.Connected += (opp, myColor) => Dispatch(() =>
        {
            OpponentName = opp;
            MyColor = myColor;
            PrepareOnline();
        });

        session.MoveReceived += (x, y) => Dispatch(() =>
        {
            if (!GameOver && Core.IsEmpty(x, y)) DoPlace(x, y);
        });

        session.UndoRequested += () => Dispatch(async () =>
        {
            var ok = AskConfirm != null && await AskConfirm(L.T("CUndoReqTitle"), L.T("CUndoReqMsg", OpponentName));
            Session?.SendUndoResponse(ok);
            if (ok) ApplyNetUndo();
        });

        session.UndoResponse += ok => Dispatch(() =>
        {
            if (ok) { ApplyNetUndo(); Message?.Invoke(L.T("NetUndoAccepted")); }
            else Message?.Invoke(L.T("NetUndoDenied"));
        });

        session.RestartRequested += () => Dispatch(async () =>
        {
            var ok = AskConfirm != null && await AskConfirm(L.T("CRestartTitle"), L.T("CRestartMsg", OpponentName));
            Session?.SendRestartResponse(ok);
            if (ok) RestartOnline();
        });

        session.RestartResponse += ok => Dispatch(() =>
        {
            if (ok) RestartOnline();
            else Message?.Invoke(L.T("NetRestartDenied"));
        });

        session.Closed += reason => Dispatch(() =>
        {
            if (!OnlineConnected) return;
            OnlineConnected = false;
            GameOver = true;
            Message?.Invoke(reason);
            RaiseStateChanged();
        });
    }

    public void DetachSession()
    {
        var s = Session;
        Session = null;
        if (s != null)
        {
            try { s.Dispose(); } catch { }
        }
        OnlineConnected = false;
    }

    public void RequestNewGame()
    {
        if (Mode == GameMode.Online) { Session?.RequestRestart(); return; }
        StartNewGame(Mode);
    }

    private void RestartOnline()
    {
        GameOver = false;
        Winner = null;
        Draw = false;
        Core.Reset();
        GameReset?.Invoke();
        RaiseStateChanged();
    }

    // ---------- 工具 ----------

    private void Dispatch(Action action)
    {
        if (UiDispatch != null) UiDispatch(action);
        else action();
    }

    private void CancelThinking()
    {
        _aiCts?.Cancel();
        _hintCts?.Cancel();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke();
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Gomoku.Game;

namespace Gomoku.Net;

/// <summary>
/// 局域网联机会话（TCP 对局 + UDP 房间发现）。
/// 协议为 UTF-8 JSON 行：{"t":"hi"|"m"|"ur"|"uo"|"rs"|"ro"|"p", ...}。
/// 主机执黑先行；所有事件在接收线程触发，调用方需自行调度到 UI 线程。
/// </summary>
public class NetSession : IDisposable
{
    public const int GamePort = 45679;
    public const int DiscoveryPort = 45680;

    public enum NetRole { None, Host, Client }

    public class HostInfo
    {
        public string Name { get; set; } = "";
        public string Ip { get; set; } = "";
        public override string ToString() => $"{Name}（{Ip}）";
    }

    public NetRole Role { get; private set; } = NetRole.None;
    public string OpponentName { get; private set; } = "";
    public StoneColor MyColor { get; private set; } = StoneColor.Black;

    public event Action<string, StoneColor>? Connected;       // (对手名, 我方颜色)
    public event Action<int, int>? MoveReceived;
    public event Action? UndoRequested;
    public event Action<bool>? UndoResponse;
    public event Action? RestartRequested;
    public event Action<bool>? RestartResponse;
    public event Action<string>? Closed;                      // 断开原因

    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly object _writeLock = new();
    private readonly CancellationTokenSource _cts = new();
    private string _myName = "";
    private bool _closedRaised;

    // ---------- 主机 ----------

    public async Task HostAsync(string name, CancellationToken ct = default)
    {
        _myName = Sanitize(name);
        Role = NetRole.Host;

        _listener = new TcpListener(IPAddress.Any, GamePort);
        _listener.Start();

        _ = DiscoveryListenLoopAsync();   // 响应客户端广播探活，不阻塞建房
        try
        {
            var tcp = await _listener.AcceptTcpClientAsync(ct);
            _client = tcp;
            SetupStreams();

            var hello = await ReadHelloAsync(ct);             // 先收对方的问候
            await SendHelloAsync(_myName, ct);                // 再回发自己
            OpponentName = hello.Name;
            MyColor = StoneColor.Black;
            _cts.Cancel();                                    // 停止房间广播
            Connected?.Invoke(OpponentName, MyColor);
            _ = ReceiveLoopAsync();
        }
        catch (OperationCanceledException) { Close("已取消"); }
        catch (Exception) { Close("创建房间失败"); }
    }

    // ---------- 客户端 ----------

    public async Task JoinAsync(string ip, string name, CancellationToken ct = default)
    {
        _myName = Sanitize(name);
        Role = NetRole.Client;

        _client = new TcpClient();
        try
        {
            await _client.ConnectAsync(IPAddress.Parse(ip.Trim()), GamePort, ct);
            SetupStreams();
            await SendHelloAsync(_myName, ct);
            var hello = await ReadHelloAsync(ct);
            OpponentName = hello.Name;
            MyColor = StoneColor.White;
            Connected?.Invoke(OpponentName, MyColor);
            _ = ReceiveLoopAsync();
        }
        catch (OperationCanceledException) { Close("已取消"); }
        catch (Exception) { Close("无法连接到该地址，请检查 IP 与网络"); }
    }

    // ---------- 公共 ----------

    public void SendMove(int col, int row) => Send("m", x: col, y: row);
    public void RequestUndo() => Send("ur");
    public void SendUndoResponse(bool ok) => Send("uo", ok: ok);
    public void RequestRestart() => Send("rs");
    public void SendRestartResponse(bool ok) => Send("ro", ok: ok);

    private void SetupStreams()
    {
        _stream = _client!.GetStream();
        _reader = new StreamReader(_stream, Encoding.UTF8);
        _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true, NewLine = "\n" };
        try { _stream.ReadTimeout = 15_000; } catch { }
    }

    private async Task SendHelloAsync(string name, CancellationToken ct)
    {
        var line = JsonSerializer.Serialize(new { t = "hi", n = name });
        await SendLineAsync(line, ct);
    }

    private async Task<(string Name, string Ip)> ReadHelloAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? line = await _reader!.ReadLineAsync().WaitAsync(ct);
            if (line == null) throw new IOException("连接已关闭");
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.GetProperty("t").GetString() == "hi")
                return (root.GetProperty("n").GetString() ?? "对手", "");
        }
        throw new OperationCanceledException();
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await _reader!.ReadLineAsync();
                }
                catch (IOException)
                {
                    // 读超时：发心跳保活
                    Send("p");
                    continue;
                }
                if (line == null) break;
                HandleLine(line);
            }
        }
        catch (Exception) { }
        Close(_client == null ? "连接已断开" : "对方已断开连接");
    }

    private void HandleLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            switch (root.GetProperty("t").GetString())
            {
                case "m":
                    MoveReceived?.Invoke(root.GetProperty("x").GetInt32(), root.GetProperty("y").GetInt32());
                    break;
                case "ur":
                    UndoRequested?.Invoke();
                    break;
                case "uo":
                    UndoResponse?.Invoke(root.TryGetProperty("ok", out var ok) && ok.GetBoolean());
                    break;
                case "rs":
                    RestartRequested?.Invoke();
                    break;
                case "ro":
                    RestartResponse?.Invoke(root.TryGetProperty("ok", out var rok) && rok.GetBoolean());
                    break;
                case "p":
                    break;   // 心跳
            }
        }
        catch { /* 忽略无法解析的消息 */ }
    }

    private void Send(string t, int x = 0, int y = 0, bool ok = false)
    {
        var line = JsonSerializer.Serialize(new { t, x, y, ok });
        lock (_writeLock)
        {
            try { _writer?.WriteLine(line); } catch { }
        }
    }

    private Task SendLineAsync(string line, CancellationToken ct)
        => Task.Run(() => { lock (_writeLock) { _writer?.WriteLine(line); } }, ct);

    // ---------- 房间发现 ----------

    /// <summary>主机在等待对手期间监听 UDP 探活并回复，客户端据此列出房间。</summary>
    private async Task DiscoveryListenLoopAsync()
    {
        try
        {
            using var udp = new UdpClient(DiscoveryPort);
            var reply = Encoding.UTF8.GetBytes($"GOMOKU|{_myName}|");
            while (!_cts.IsCancellationRequested && _client == null)
            {
                try
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    var buf = udp.Receive(ref ep);
                    if (Encoding.UTF8.GetString(buf).StartsWith("GOMOKU_PROBE", StringComparison.Ordinal))
                        await udp.SendAsync(reply, reply.Length, ep);
                }
                catch (SocketException) { /* 超时继续 */ }
                catch (ObjectDisposedException) { break; }
            }
        }
        catch { /* 45680 被占用（如第二个房间）时静默降级为仅手动 IP 加入 */ }
    }

    /// <summary>广播探活并收集 2.5 秒内的房间回复（阻塞式，请在后台任务调用）。</summary>
    public static Task<List<HostInfo>> DiscoverAsync(int timeoutMs = 2500) => Task.Run(() =>
    {
        var results = new List<HostInfo>();
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
            udp.Client.ReceiveTimeout = 300;
            var probe = Encoding.UTF8.GetBytes("GOMOKU_PROBE");
            udp.Send(probe, probe.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));

            long deadline = Environment.TickCount64 + timeoutMs;
            while (Environment.TickCount64 < deadline)
            {
                try
                {
                    var ep = new IPEndPoint(IPAddress.Any, 0);
                    var buf = udp.Receive(ref ep);
                    var text = Encoding.UTF8.GetString(buf);
                    if (text.StartsWith("GOMOKU|", StringComparison.Ordinal))
                    {
                        var parts = text.Split('|');
                        if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[1]))
                            results.Add(new HostInfo { Name = parts[1], Ip = ep.Address.ToString() });
                    }
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }
            }
        }
        catch { /* 无网络适配器等情况 */ }
        return results.DistinctBy(h => h.Ip).ToList();
    });

    // ---------- 收尾 ----------

    private static string Sanitize(string name)
    {
        name = name?.Trim() ?? "玩家";
        if (string.IsNullOrEmpty(name)) name = "玩家";
        if (name.Length > 20) name = name[..20];
        return name.Replace("|", "·").Replace("\n", " ").Replace("\r", " ");
    }

    public void Close(string reason = "")
    {
        if (_closedRaised && Role == NetRole.None) return;
        _closedRaised = true;
        _cts.Cancel();
        try { _writer?.WriteLine(JsonSerializer.Serialize(new { t = "bye" })); } catch { }
        try { _client?.Close(); } catch { }
        try { _listener?.Stop(); } catch { }
        Role = NetRole.None;
        Closed?.Invoke(reason);
    }

    public void Dispose() => Close("会话已关闭");
}

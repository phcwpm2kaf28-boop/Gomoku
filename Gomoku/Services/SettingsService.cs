using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Gomoku.Services;

/// <summary>应用主题</summary>
public enum AppTheme { System, Light, Dark }

/// <summary>游戏动作（用于键位绑定）</summary>
public enum GameAction
{
    MoveUp, MoveDown, MoveLeft, MoveRight,
    PlaceStone, Undo, Hint, NewGame, Theme, Settings,
}

/// <summary>
/// 设置持久化：%LOCALAPPDATA%\Gomoku\settings.json
/// </summary>
public class SettingsService
{
    /// <summary>默认昵称：Windows 当前登录账户名（本地账户 / 微软账户）</summary>
    public static string DefaultPlayerName()
    {
        var name = Environment.UserName;
        return string.IsNullOrWhiteSpace(name) ? "玩家" : name;
    }

    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool HintsEnabled { get; set; } = true;
    public string PlayerName { get; set; } = DefaultPlayerName();
    /// <summary>界面语言："System" 跟随系统，或 "en" / "zh-CN" / "fr-FR" 等。</summary>
    public string Language { get; set; } = "System";
    public int LastMode { get; set; }
    public int LastDifficulty { get; set; } = 2;
    public bool LastPlayerIsBlack { get; set; } = true;
    public Dictionary<string, List<string>> KeyBindings { get; set; } = Gomoku.Services.KeyBindings.Defaults();

    public string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Gomoku", "settings.json");

    public void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            var loaded = JsonSerializer.Deserialize<SettingsService>(json);
            if (loaded == null) return;
            // 仅覆盖合法字段；键位用默认值做底，兼容新版本新增动作
            Theme = loaded.Theme;
            HintsEnabled = loaded.HintsEnabled;
            // 旧版本默认昵称"玩家"视为未自定义，迁移为 Windows 账户名
            if (!string.IsNullOrWhiteSpace(loaded.PlayerName))
                PlayerName = loaded.PlayerName == "玩家" ? DefaultPlayerName() : loaded.PlayerName;
            if (!string.IsNullOrWhiteSpace(loaded.Language)) Language = loaded.Language;
            LastMode = loaded.LastMode;
            LastDifficulty = loaded.LastDifficulty;
            LastPlayerIsBlack = loaded.LastPlayerIsBlack;
            if (loaded.KeyBindings != null && loaded.KeyBindings.Count > 0)
            {
                var merged = Gomoku.Services.KeyBindings.Defaults();
                foreach (var kv in loaded.KeyBindings)
                    if (merged.ContainsKey(kv.Key) && kv.Value is { Count: > 0 })
                        merged[kv.Key] = kv.Value;
                KeyBindings = merged;
            }
        }
        catch { /* 配置损坏时使用默认值 */ }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 保存失败不影响运行 */ }
    }
}

/// <summary>默认键位（可在设置页自定义）</summary>
public static class KeyBindings
{
    public static readonly (GameAction Action, string Label)[] All =
    {
        (GameAction.MoveUp, "光标上移"),
        (GameAction.MoveDown, "光标下移"),
        (GameAction.MoveLeft, "光标左移"),
        (GameAction.MoveRight, "光标右移"),
        (GameAction.PlaceStone, "落子"),
        (GameAction.Undo, "悔棋"),
        (GameAction.Hint, "提示"),
        (GameAction.NewGame, "新开局"),
        (GameAction.Theme, "切换主题"),
        (GameAction.Settings, "打开设置"),
    };

    public static Dictionary<string, List<string>> Defaults() => new()
    {
        ["MoveUp"] = new() { "Up", "W" },
        ["MoveDown"] = new() { "Down", "S" },
        ["MoveLeft"] = new() { "Left", "A" },
        ["MoveRight"] = new() { "Right", "D" },
        ["PlaceStone"] = new() { "Enter", "Space" },
        ["Undo"] = new() { "U" },
        ["Hint"] = new() { "H" },
        ["NewGame"] = new() { "R" },
        ["Theme"] = new() { "T" },
        ["Settings"] = new() { "O" },
    };

    public static string KeyName(GameAction action) => action.ToString();
}

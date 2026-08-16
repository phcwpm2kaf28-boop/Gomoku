using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Gomoku.Services;

/// <summary>键位字符串与 KeyRoutedEventArgs 之间的转换 / 匹配工具。</summary>
public static class KeyUtil
{
    public static bool IsDown(VirtualKey key)
    {
        try
        {
            var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
            return state.HasFlag(CoreVirtualKeyStates.Down);
        }
        catch { return false; }
    }

    /// <summary>把按键事件转成规范字符串，如 "Ctrl+U" / "Left" / "Space"。</summary>
    public static string KeyToString(KeyRoutedEventArgs e)
    {
        string mods = "";
        if (IsDown(VirtualKey.Control)) mods += "Ctrl+";
        if (IsDown(VirtualKey.Shift)) mods += "Shift+";
        if (IsDown(VirtualKey.Menu)) mods += "Alt+";
        return mods + e.Key.ToString();
    }

    /// <summary>友好显示名，如 "↑" / "空格"。</summary>
    public static string Display(string binding)
    {
        var parts = binding.Split('+');
        var key = parts[^1];
        string display = key switch
        {
            "Up" => "↑",
            "Down" => "↓",
            "Left" => "←",
            "Right" => "→",
            "Space" => "空格",
            "Enter" => "回车",
            "Escape" => "Esc",
            "Back" => "退格",
            "Tab" => "Tab",
            "Delete" => "Delete",
            _ => key.Length == 1 ? key.ToUpperInvariant() : key,
        };
        return parts.Length > 1 ? string.Join("+", parts.Take(parts.Length - 1)) + "+" + display : display;
    }

    /// <summary>判断某按键绑定是否命中该按键事件（含修饰键匹配）。</summary>
    public static bool Matches(IReadOnlyList<string> bindings, KeyRoutedEventArgs e)
    {
        if (bindings == null) return false;
        foreach (var binding in bindings)
        {
            if (Matches(binding, e)) return true;
        }
        return false;
    }

    public static bool Matches(string binding, KeyRoutedEventArgs e)
    {
        var parts = binding.Split('+');
        if (!e.Key.ToString().Equals(parts[^1], StringComparison.OrdinalIgnoreCase)) return false;
        bool wantCtrl = parts.Contains("Ctrl");
        bool wantShift = parts.Contains("Shift");
        bool wantAlt = parts.Contains("Alt");
        if (wantCtrl != IsDown(VirtualKey.Control)) return false;
        if (wantShift != IsDown(VirtualKey.Shift)) return false;
        if (wantAlt != IsDown(VirtualKey.Menu)) return false;
        return true;
    }
}

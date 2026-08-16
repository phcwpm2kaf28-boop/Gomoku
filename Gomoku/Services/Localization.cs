using System.Globalization;
using System.Resources;

namespace Gomoku.Services;

/// <summary>
/// 本地化服务：.resx 资源字典 + 运行时语言切换。
/// 界面语言默认跟随系统（Settings.Language = "System"），也可在设置页手动选择；
/// 切换后立即生效、无需重启（所有界面文本统一通过 T() 获取并刷新）。
/// </summary>
public static class L
{
    private static readonly ResourceManager Rm =
        new("Gomoku.Resources.Resources", typeof(L).Assembly);

    /// <summary>取本地化字符串；支持 {0}/{1} 占位符；找不到键时回退显示键名。</summary>
    public static string T(string key, params object?[] args)
    {
        var s = Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        return args.Length == 0 ? s : string.Format(s, args);
    }

    /// <summary>应用界面语言（"System" 表示跟随系统，或 "en" / "zh-CN" / "fr-FR" 等代码）。</summary>
    public static void Apply(string? lang)
    {
        var ci = string.IsNullOrEmpty(lang) || lang == "System"
            ? CultureInfo.InstalledUICulture
            : new CultureInfo(lang);
        CultureInfo.CurrentUICulture = ci;
        CultureInfo.DefaultThreadCurrentUICulture = ci;
    }

    /// <summary>可选界面语言：语言代码 → 该语言的原生名称（"System" 的显示名见 LangSystem 资源键）。</summary>
    public static readonly (string Code, string Native)[] Languages =
    {
        ("System", "System"),
        ("en", "English"),
        ("zh-CN", "简体中文"),
        ("fr-FR", "Français"),
        ("de-DE", "Deutsch"),
        ("es-ES", "Español"),
        ("ja-JP", "日本語"),
        ("ko-KR", "한국어"),
        ("ru-RU", "Русский"),
    };
}

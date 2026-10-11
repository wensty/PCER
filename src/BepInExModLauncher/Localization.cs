using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace BepInExModLauncher;

internal static class L
{
    private static readonly CultureInfo SystemCulture = CultureInfo.CurrentUICulture;
    internal static readonly Dictionary<string, string> Chinese = LoadChinese();
    internal static string Language { get; private set; } = "en";
    internal static bool ExpandForTesting;
    public static void SetLanguage(string preference)
    {
        Language = ResolveLanguage(preference, SystemCulture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(Language);
    }
    internal static string ResolveLanguage(string preference, CultureInfo systemCulture) =>
        preference == "zh-CN" || (preference == "auto" && systemCulture.TwoLetterISOLanguageName == "zh") ? "zh-CN" : "en";
    public static string T(string english, params object[] args)
    {
        var translated = Language == "zh-CN" && Chinese.TryGetValue(english, out var value) ? value : english;
        var text = args.Length == 0 ? translated : string.Format(CultureInfo.CurrentCulture, translated, args);
        return ExpandForTesting ? text + " [" + text + "]" : text;
    }
    private static Dictionary<string, string> LoadChinese()
    {
        using (var stream = typeof(L).Assembly.GetManifestResourceStream("BepInExModLauncher.Localization.zh-CN.json"))
        using (var reader = new StreamReader(stream))
            return new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
    }
}

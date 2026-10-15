using System;
namespace YuiPhysicalAI.LocalAI
{
    // Checks the input/output path, not model intelligence or speed.
    public static class YuiLocalModelProbe
    {
        public static string NewCode() => "YUI_CHECK_"+Guid.NewGuid().ToString("N").Substring(0,12).ToUpperInvariant();
        public static string Prompt(string code) => "Repeat this verification code exactly, without explanation: "+code;
        public static bool Matches(string text,string code) => !string.IsNullOrEmpty(code)
            && YuiLlamaRuntime.StripReasoning(text).IndexOf(code,StringComparison.Ordinal)>=0;
    }
}

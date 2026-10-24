namespace MacKeys;

public sealed record ShortcutRemap(string From, string To, string Meaning, string OriginalKeys, string NewRemapKeys)
{
    public string Display => $"{From}  →  {To}    {Meaning}";
}

public static class RemapCatalog
{
    public const string CapsLockCode = "20";
    public const string HangulCode = "21";
    public const string AlwaysAppliedDisplay = "Caps Lock  →  한/영";

    public static readonly IReadOnlyList<ShortcutRemap> Shortcuts = new ShortcutRemap[]
    {
        new("Alt+C", "Ctrl+C", "복사", "164;67", "162;67"),
        new("Alt+V", "Ctrl+V", "붙여넣기", "164;86", "162;86"),
        new("Alt+X", "Ctrl+X", "잘라내기", "164;88", "162;88"),
        new("Alt+Z", "Ctrl+Z", "실행 취소", "164;90", "162;90"),
        new("Alt+Shift+Z", "Ctrl+Y", "다시 실행", "164;160;90", "162;89"),
        new("Alt+S", "Ctrl+S", "저장", "164;83", "162;83"),
        new("Alt+W", "Ctrl+W", "닫기", "164;87", "162;87"),
        new("Alt+A", "Ctrl+A", "모두 선택", "164;65", "162;65"),
        new("Alt+F", "Ctrl+F", "찾기", "164;70", "162;70"),
        new("Alt+N", "Ctrl+N", "새로 만들기", "164;78", "162;78"),
        new("Alt+T", "Ctrl+T", "새 탭", "164;84", "162;84"),
        new("Alt+Shift+T", "Ctrl+Shift+T", "닫은 탭 다시 열기", "164;160;84", "162;160;84"),
        new("Alt+R", "Ctrl+R", "새로고침", "164;82", "162;82"),
        new("Alt+Q", "Alt+F4", "종료", "164;81", "164;115"),
        new("Alt+←", "Home", "홈", "164;37", "36"),
        new("Alt+→", "End", "엔드", "164;39", "35"),
        new("Alt+↑", "Page Up", "페이지 업", "164;38", "33"),
        new("Alt+↓", "Page Down", "페이지 다운", "164;40", "34"),
    };
}

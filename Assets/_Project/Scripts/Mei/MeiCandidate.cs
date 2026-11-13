/// <summary>銘の選択画面の種類</summary>
public enum MeiChoiceKind
{
    /// <summary>小銘(場外に落とした数が一定に達したとき)</summary>
    Minor,
    /// <summary>大銘(ウェーブをクリアしたとき)</summary>
    Major,
}

/// <summary>
/// 選択画面に並べる候補1枚分。画面(UI)は銘の仕組みを知らずに、この中身を表示するだけにする。
/// </summary>
public readonly struct MeiCandidate
{
    public readonly MeiChoiceKind Kind;
    public readonly MinorMeiId Minor;
    public readonly MajorMeiId Major;
    /// <summary>「今の大銘を残す」の候補か</summary>
    public readonly bool IsKeep;
    public readonly string DisplayName;
    /// <summary>1行の説明(仕様:効果は1行で説明できるものに限る)</summary>
    public readonly string Description;
    /// <summary>補足。小銘なら「線の幅 ×1.25 から ×1.50 へ」、大銘なら刀の光り方</summary>
    public readonly string Detail;
    /// <summary>小銘の今の段数(大銘では 0)</summary>
    public readonly int CurrentStage;
    public readonly int MaxStage;
    /// <summary>代償つきの小銘か(カードに「代償つき」と出すため)</summary>
    public readonly bool IsCost;

    public MeiCandidate(MeiChoiceKind kind, MinorMeiId minor, MajorMeiId major, bool isKeep,
        string displayName, string description, string detail, int currentStage, int maxStage, bool isCost = false)
    {
        IsCost = isCost;
        Kind = kind;
        Minor = minor;
        Major = major;
        IsKeep = isKeep;
        DisplayName = displayName;
        Description = description;
        Detail = detail;
        CurrentStage = currentStage;
        MaxStage = maxStage;
    }
}

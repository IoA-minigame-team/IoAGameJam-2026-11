/// <summary>画面に出す漢数字(第三波、五体など)。0〜99 まで</summary>
public static class KanjiNumber
{
    static readonly string[] DIGITS = { "〇", "一", "二", "三", "四", "五", "六", "七", "八", "九" };

    public static string Of(int n)
    {
        if (n < 0 || n > 99) return n.ToString();
        if (n < 10) return DIGITS[n];
        int tens = n / 10, ones = n % 10;
        string head = tens == 1 ? "十" : DIGITS[tens] + "十";
        return ones == 0 ? head : head + DIGITS[ones];
    }
}

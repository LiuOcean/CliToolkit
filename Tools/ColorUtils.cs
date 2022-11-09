namespace CliToolkit.Tools;

public enum CustomColor
{
    Blue,
    Black,
    Red,
    Gray,
    Pink
}

public static class ColorUtils
{
    public static string WithColor(this string self, string hex) { return$"[{hex}]{self}[/]"; }

    public static string WithColor(this string self, CustomColor color) { return self.WithColor(color.ToHex()); }

    public static string ToHex(this CustomColor color)
    {
        switch(color)
        {
            case CustomColor.Blue:  return"#08d9d6";
            case CustomColor.Black: return"#252a34";
            case CustomColor.Red:   return"#ff2e63";
            case CustomColor.Gray:  return"#393e46";
            case CustomColor.Pink:  return"#fcbad3";
            default:                throw new ArgumentOutOfRangeException(nameof(color), color, null);
        }
    }
}
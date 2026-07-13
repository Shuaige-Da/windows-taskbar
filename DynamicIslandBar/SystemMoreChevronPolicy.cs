namespace DynamicIslandBar;

/// <summary>
/// Keeps the system expansion indicator pointing toward the popup's opening direction.
/// The source glyph points down at zero degrees.
/// </summary>
public static class SystemMoreChevronPolicy
{
    public static double ResolveAngle(PopupFlowDirection direction) => direction switch
    {
        PopupFlowDirection.Up => 180d,
        PopupFlowDirection.Left => 90d,
        PopupFlowDirection.Right => -90d,
        _ => 0d
    };
}

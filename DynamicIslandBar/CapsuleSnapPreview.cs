namespace DynamicIslandBar;

public enum SnapEdge
{
    None,
    Top,
    Bottom,
    Left,
    Right
}

public readonly record struct CapsuleSnapPreview(
    SnapEdge Edge,
    CapsuleMode Mode,
    double CapsuleWidth,
    double CapsuleHeight,
    double RotationDegrees,
    WindowFrame Frame);

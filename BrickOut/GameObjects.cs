using System.Numerics;

namespace EscapeMine;

public sealed class Ball
{
    public Vector2 Position { get; internal set; }
    public Vector2 Velocity { get; internal set; }
    public int Power { get; internal set; }
}

public sealed class Pickaxe
{
    public float X { get; internal set; } = Rules.Width / 2;
    public bool Strong { get; internal set; }
    public float Flash { get; internal set; }
    public bool LastStrong { get; internal set; }
    public float HalfWidth => Strong ? 36 : 88;
}

public sealed class Rock
{
    public int Column { get; init; }
    public int Row { get; init; }
    public bool Hard { get; init; }
    public int Health { get; internal set; }
    public bool Tank { get; internal set; }
    public float X => Rules.FieldX + Column * (Rules.Tile + Rules.Gap);
    public float Y => Rules.FieldY + Row * (Rules.Tile + Rules.Gap);
}

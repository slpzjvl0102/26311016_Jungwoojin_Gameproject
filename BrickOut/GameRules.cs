namespace EscapeMine;

public static class Rules
{
    public const float Width = 1200, Height = 900, PaddleY = 750, Floor = 804;
    public const float Radius = 11, Speed = 510, OxygenMax = 100, Drain = .25f;
    public const float NormalCost = 1, StrongCost = 2.5f, TankRecovery = 20;
    public const int Columns = 18, Rows = 8;
    public const float Tile = 64, FieldX = 0, FieldY = 60;
    public const float Gap = (Width - Columns * Tile) / (Columns - 1);
    public const float FieldRight = FieldX + Columns * (Tile + Gap) - Gap;
    public const float FieldBottom = FieldY + Rows * (Tile + Gap) - Gap;
    public const float Step = 1f / 240;
}

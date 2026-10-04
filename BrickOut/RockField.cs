namespace EscapeMine;

internal static class RockField
{
    public static List<Rock> Create(Random random)
    {
        List<Rock> rocks = new();
        for (int row = 0; row < Rules.Rows; row++)
        for (int col = 0; col < Rules.Columns; col++)
        {
            // 최상단은 반드시 돌. 나머지 행은 산소통 하나씩을 빈 칸에 배치합니다.
            bool tank = row > 0 && col == (row * 5 + 2) % Rules.Columns;
            bool hard = random.NextDouble() < .27;
            rocks.Add(new Rock { Row = row, Column = col, Hard = hard,
                Health = tank ? 0 : hard ? 2 : 1, Tank = tank });
        }
        return rocks;
    }
}

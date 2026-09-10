/// <summary>独立教学棋盘，不读写正式关卡，也不消耗生命或道具。行列均从左上角开始。</summary>
public sealed class MPGuideLesson
{
    public enum Stage
    {
        Welcome, FillSwitch, FullRow, Consecutive, Groups, Order,
        Columns, BlankSwitch, Blanks, LastSwitch, LastCell, Completed
    }

    public const int Size = 5;
    private static readonly string[] Answer = { "11111", "11110", "11011", "10111", "00100" };
    private static readonly string[] RowHints = { "5", "4", "2  2", "1  3", "1" };
    private static readonly string[] ColumnHints = { "4", "3", "2\n2", "4", "1\n2" };
    private readonly bool[] m_marked = new bool[Size * Size];

    public Stage Current { get; private set; }
    public bool FillMode { get; private set; }
    public int VisibleRows => Current <= Stage.FullRow ? 1 : Current == Stage.Consecutive ? 2 :
        Current == Stage.Groups ? 3 : Current == Stage.Order ? 4 : Size;
    public bool ShowColumns => Current >= Stage.Columns;
    public bool CanSwitch => Current == Stage.FillSwitch || Current == Stage.BlankSwitch || Current == Stage.LastSwitch;
    public bool IsMarked(int index) => index >= 0 && index < m_marked.Length && m_marked[index];
    public static bool IsFill(int index) => index >= 0 && index < Size * Size && Answer[index / Size][index % Size] == '1';
    public static string RowHint(int row) => RowHints[row];
    public static string ColumnHint(int column) => ColumnHints[column];

    public string Description
    {
        get
        {
            switch (Current)
            {
                case Stage.Welcome: return "Welcome to MagicPixel!\nUse the numbers to reveal a hidden picture.\nLet's learn by playing a small puzzle.";
                case Stage.FillSwitch: return "Fill squares to reveal the picture.\nTap the switch below to select <color=#D58A13>Fill mode</color>.";
                case Stage.FullRow: return "The number <color=#D58A13>5</color> means five filled squares in a row.\nPress and slide across all five squares.";
                case Stage.Consecutive: return "<color=#D58A13>4</color> means four consecutive filled squares.\nThe first is filled. Slide through the next three.\nLeave the last square empty.";
                case Stage.Groups: return "<color=#D58A13>2  2</color> means two groups of two filled squares.\nLeave at least one empty square between groups.\nSlide across each highlighted group.";
                case Stage.Order: return "<color=#D58A13>1  3</color> means one square, then a group of three.\nFollow the order: left to right in rows,\nand top to bottom in columns.";
                case Stage.Columns: return "Read the numbers above each column, too.\nColumn 3 has <color=#D58A13>2  2</color>: two groups of two, with a gap.\nUse row and column clues together.";
                case Stage.BlankSwitch: return "Mark squares that must stay empty with an <color=#D58A13>X</color>.\nTap the switch to select X mode.";
                case Stage.Blanks: return "Tap each highlighted empty square to mark an X.\nIn normal levels, empty squares are marked\nautomatically when a row or column is filled.";
                case Stage.LastSwitch: return "The last row needs <color=#D58A13>1</color> filled square.\nColumn 3 shows exactly where it belongs.\nSwitch back to Fill mode.";
                case Stage.LastCell: return "Fill the last square to reveal your picture!\nIn normal levels, mistakes cost a life.\nUse the clues before you fill.";
                default: return "Well done! Your first picture is complete.";
            }
        }
    }

    public bool IsTarget(int index)
    {
        if (index < 0 || index >= m_marked.Length || m_marked[index]) return false;
        int row = index / Size;
        switch (Current)
        {
            case Stage.FullRow: return row == 0;
            case Stage.Consecutive: return row == 1 && IsFill(index);
            case Stage.Groups: return row == 2 && IsFill(index);
            case Stage.Order: return row == 3 && IsFill(index);
            case Stage.Blanks: return !IsFill(index);
            case Stage.LastCell: return index == 22;
            default: return false;
        }
    }

    public int FirstTarget()
    {
        for (int i = 0; i < m_marked.Length; i++) if (IsTarget(i)) return i;
        return -1;
    }

    public bool TryMark(int index)
    {
        if (!IsTarget(index) || FillMode != IsFill(index)) return false;
        m_marked[index] = true;
        return true;
    }

    public bool SwitchMode()
    {
        if (!CanSwitch) return false;
        FillMode = !FillMode;
        Current++;
        return true;
    }

    public bool Advance()
    {
        if (Current == Stage.Completed) return false;
        // 操作步骤必须真实完成；说明步骤由按钮继续。
        if (FirstTarget() >= 0 || CanSwitch) return false;
        Current++;
        if (Current == Stage.Consecutive) m_marked[5] = true;
        return true;
    }

    public bool LineFilled(bool column, int line)
    {
        for (int i = 0; i < Size; i++)
        {
            int index = column ? i * Size + line : line * Size + i;
            if (IsFill(index) && !m_marked[index]) return false;
        }
        return true;
    }
}

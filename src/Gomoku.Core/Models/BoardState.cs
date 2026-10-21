namespace Gomoku.Core.Models;

/// <summary>
/// Immutable snapshot or active representation of a Gomoku board position.
/// </summary>
public sealed class BoardState
{
    private readonly PieceColor[] _cells;

    public BoardSize Size { get; }
    public GomokuVariant Variant { get; }
    public PieceColor SideToMove { get; internal set; }
    public PieceColor FirstPlayer { get; }
    public int MoveCount { get; internal set; }
    public ulong Hash { get; internal set; }
    public GameStatus Status { get; internal set; }
    public IReadOnlyList<Point> WinningLine { get; internal set; }

    public BoardState(BoardSize size, GomokuVariant variant = GomokuVariant.Freestyle, PieceColor firstPlayer = PieceColor.Black)
    {
        Size = size;
        Variant = variant;
        FirstPlayer = firstPlayer;
        SideToMove = firstPlayer;
        MoveCount = 0;
        Status = GameStatus.InProgress;
        WinningLine = Array.Empty<Point>();
        _cells = new PieceColor[size.TotalCells];
        Array.Fill(_cells, PieceColor.Empty);
    }

    private BoardState(BoardState source)
    {
        Size = source.Size;
        Variant = source.Variant;
        SideToMove = source.SideToMove;
        FirstPlayer = source.FirstPlayer;
        MoveCount = source.MoveCount;
        Hash = source.Hash;
        Status = source.Status;
        WinningLine = source.WinningLine;
        _cells = (PieceColor[])source._cells.Clone();
    }

    public BoardState Clone() => new(this);

    public PieceColor GetCell(int x, int y)
    {
        if (x < 0 || x >= Size.Width || y < 0 || y >= Size.Height)
            return PieceColor.Wrong;
        return _cells[y * Size.Width + x];
    }

    public PieceColor GetCell(Point point) => GetCell(point.X, point.Y);

    internal void SetCell(int x, int y, PieceColor color)
    {
        if (x >= 0 && x < Size.Width && y >= 0 && y < Size.Height)
        {
            _cells[y * Size.Width + x] = color;
        }
    }

    public bool IsOccupied(int x, int y)
    {
        PieceColor c = GetCell(x, y);
        return c is PieceColor.Black or PieceColor.White;
    }

    public bool IsEmpty(int x, int y) => GetCell(x, y) == PieceColor.Empty;

    public int RemainingVacantCells => Size.TotalCells - MoveCount;

    public byte[] ToCellBytes()
    {
        byte[] bytes = new byte[_cells.Length];
        for (int i = 0; i < _cells.Length; i++)
        {
            bytes[i] = (byte)_cells[i];
        }
        return bytes;
    }

    public static BoardState FromCellBytes(
        BoardSize size,
        GomokuVariant variant,
        PieceColor firstPlayer,
        PieceColor sideToMove,
        int moveCount,
        ulong hash,
        GameStatus status,
        ReadOnlySpan<byte> cells)
    {
        var state = new BoardState(size, variant, firstPlayer)
        {
            SideToMove = sideToMove,
            MoveCount = moveCount,
            Hash = hash,
            Status = status
        };
        int len = Math.Min(cells.Length, state._cells.Length);
        for (int i = 0; i < len; i++)
        {
            state._cells[i] = (PieceColor)cells[i];
        }
        return state;
    }
}


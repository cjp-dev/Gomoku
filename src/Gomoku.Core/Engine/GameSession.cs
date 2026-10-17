using Gomoku.Core.Models;

namespace Gomoku.Core.Engine;

/// <summary>
/// Coordinates a game session, managing move history, undo/redo, and Zobrist hash history.
/// </summary>
public sealed class GameSession
{
    private readonly IRuleEngine _rules;
    private readonly List<Move> _history = new();
    private readonly Stack<Move> _redoStack = new();
    private readonly List<ulong> _hashHistory = new();

    public BoardState CurrentState { get; private set; }
    public IReadOnlyList<Move> MoveHistory => _history;
    public int RedoCount => _redoStack.Count;
    public IReadOnlyList<ulong> StateHashHistory => _hashHistory;

    public GameSession(
        BoardSize? size = null,
        GomokuVariant variant = GomokuVariant.Freestyle,
        PieceColor firstPlayer = PieceColor.Black,
        IRuleEngine? rules = null)
    {
        _rules = rules ?? RuleEngine.Instance;
        BoardSize actualSize = size ?? BoardSize.Default;
        CurrentState = new BoardState(actualSize, variant, firstPlayer);
        CurrentState.Hash = Zobrist.ComputeHash(CurrentState);
        _hashHistory.Add(CurrentState.Hash);
    }

    public bool CanApplyMove(Move move) => _rules.IsMoveLegal(CurrentState, move, out _);

    public bool ApplyMove(Move move)
    {
        if (!_rules.IsMoveLegal(CurrentState, move, out _)) return false;

        CurrentState = _rules.ApplyMove(CurrentState, move);
        _history.Add(move);
        _redoStack.Clear();
        _hashHistory.Add(CurrentState.Hash);
        return true;
    }

    public bool CanUndo => _history.Count > 0;

    public bool Undo()
    {
        if (!CanUndo) return false;

        Move last = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        _redoStack.Push(last);
        _hashHistory.RemoveAt(_hashHistory.Count - 1);

        // Replay from root to reconstruct state cleanly
        BoardState fresh = new(CurrentState.Size, CurrentState.Variant, CurrentState.FirstPlayer);
        fresh.Hash = Zobrist.ComputeHash(fresh);
        foreach (Move m in _history)
        {
            fresh = _rules.ApplyMove(fresh, m);
        }

        CurrentState = fresh;
        return true;
    }

    public bool CanRedo => _redoStack.Count > 0;

    public bool Redo()
    {
        if (!CanRedo) return false;

        Move next = _redoStack.Pop();
        CurrentState = _rules.ApplyMove(CurrentState, next);
        _history.Add(next);
        _hashHistory.Add(CurrentState.Hash);
        return true;
    }

    public void Reset(BoardSize size, GomokuVariant variant = GomokuVariant.Freestyle, PieceColor firstPlayer = PieceColor.Black)
    {
        _history.Clear();
        _redoStack.Clear();
        _hashHistory.Clear();
        CurrentState = new BoardState(size, variant, firstPlayer);
        CurrentState.Hash = Zobrist.ComputeHash(CurrentState);
        _hashHistory.Add(CurrentState.Hash);
    }
}


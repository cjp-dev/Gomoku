using System.Text.Json;
using System.Text.Json.Serialization;
using Gomoku.App.Models;
using Gomoku.App.Services;
using Gomoku.Core.AI;
using Gomoku.Core.Models;

namespace Gomoku.Web.Worker;

internal sealed record MoveRequest(
    byte[] Cells,
    int BoardWidth,
    int BoardHeight,
    GomokuVariant Variant,
    PieceColor SideToMove,
    PieceColor FirstPlayer,
    int MoveCount,
    ulong Hash,
    GameStatus Status,
    AiEngineType EngineType,
    TimeControlMode Mode,
    int Depth,
    long TimeTicks,
    bool UseOpeningBook,
    int TranspositionTableEntries,
    bool MultiThreadingEnabled = true,
    int ThreadCount = 0);

internal sealed record ProgressDto(
    int MoveNumber,
    int Depth,
    int Score,
    int? BestMoveX,
    int? BestMoveY,
    int? BestMovePlayer,
    long Nodes,
    long EvaluationsPerSec,
    long ElapsedMs,
    string? StatusMessage,
    bool IsSearching,
    int? CurrentMoveX = null,
    int? CurrentMoveY = null,
    int? CurrentMovePlayer = null);

internal sealed record MoveResponse(
    int MoveX,
    int MoveY,
    int MovePlayer,
    ProgressDto? LastAnalysis);

[JsonSerializable(typeof(MoveRequest))]
[JsonSerializable(typeof(ProgressDto))]
[JsonSerializable(typeof(MoveResponse))]
[JsonSerializable(typeof(AppPreferences))]
internal sealed partial class EngineProtocolJson : JsonSerializerContext;

internal static class EngineProtocol
{
    public static string WriteRequest(
        BoardState state,
        SearchLimits limits,
        GameSettings settings)
    {
        var req = new MoveRequest(
            state.ToCellBytes(),
            state.Size.Width,
            state.Size.Height,
            state.Variant,
            state.SideToMove,
            state.FirstPlayer,
            state.MoveCount,
            state.Hash,
            state.Status,
            settings.AiEngine,
            limits.Mode,
            limits.Depth,
            limits.Time.Ticks,
            limits.UseOpeningBook,
            settings.TranspositionTableEntries,
            settings.MultiThreadingEnabled,
            settings.ThreadCount);
        return JsonSerializer.Serialize(req, EngineProtocolJson.Default.MoveRequest);
    }

    public static (BoardState State, SearchLimits Limits, AiEngineType EngineType, int TtEntries, bool MultiThreadingEnabled, int ThreadCount) ReadRequest(string json)
    {
        MoveRequest req = JsonSerializer.Deserialize(json, EngineProtocolJson.Default.MoveRequest)!;
        BoardSize size = new(req.BoardWidth, req.BoardHeight);
        BoardState state = BoardState.FromCellBytes(
            size,
            req.Variant,
            req.FirstPlayer,
            req.SideToMove,
            req.MoveCount,
            req.Hash,
            req.Status,
            req.Cells);

        TimeSpan time = TimeSpan.FromTicks(req.TimeTicks);
        SearchLimits limits = req.Mode switch
        {
            TimeControlMode.FixedDepth => SearchLimits.FixedDepth(req.Depth, req.UseOpeningBook),
            TimeControlMode.TimePerMove => SearchLimits.TimePerMove(time, req.UseOpeningBook),
            _ => SearchLimits.TimePerGame(time > TimeSpan.Zero ? time : TimeSpan.FromMilliseconds(10), req.UseOpeningBook)
        };

        return (state, limits, req.EngineType, req.TranspositionTableEntries, req.MultiThreadingEnabled, req.ThreadCount);
    }

    public static string WriteProgress(SearchAnalysis info)
    {
        var dto = new ProgressDto(
            info.MoveNumber,
            info.Depth,
            info.Score,
            info.BestMove?.X,
            info.BestMove?.Y,
            info.BestMove.HasValue ? (int)info.BestMove.Value.Player : null,
            info.Nodes,
            info.EvaluationsPerSec,
            (long)info.Elapsed.TotalMilliseconds,
            info.StatusMessage,
            info.IsSearching,
            info.CurrentMove?.X,
            info.CurrentMove?.Y,
            info.CurrentMove.HasValue ? (int)info.CurrentMove.Value.Player : null);
        return JsonSerializer.Serialize(dto, EngineProtocolJson.Default.ProgressDto);
    }

    public static SearchAnalysis ReadProgress(string json)
    {
        ProgressDto dto = JsonSerializer.Deserialize(json, EngineProtocolJson.Default.ProgressDto)!;
        return new SearchAnalysis(
            dto.MoveNumber,
            dto.Depth,
            dto.Score,
            dto.BestMoveX.HasValue && dto.BestMoveY.HasValue && dto.BestMovePlayer.HasValue
                ? new Move(dto.BestMoveX.Value, dto.BestMoveY.Value, (PieceColor)dto.BestMovePlayer.Value)
                : null,
            dto.Nodes,
            dto.EvaluationsPerSec,
            TimeSpan.FromMilliseconds(dto.ElapsedMs),
            dto.StatusMessage,
            dto.IsSearching,
            dto.CurrentMoveX.HasValue && dto.CurrentMoveY.HasValue && dto.CurrentMovePlayer.HasValue
                ? new Move(dto.CurrentMoveX.Value, dto.CurrentMoveY.Value, (PieceColor)dto.CurrentMovePlayer.Value)
                : null);
    }

    public static string WriteResponse(Move move, SearchAnalysis? last)
    {
        ProgressDto? dto = last is null ? null : new ProgressDto(
            last.MoveNumber,
            last.Depth,
            last.Score,
            last.BestMove?.X,
            last.BestMove?.Y,
            last.BestMove.HasValue ? (int)last.BestMove.Value.Player : null,
            last.Nodes,
            last.EvaluationsPerSec,
            (long)last.Elapsed.TotalMilliseconds,
            last.StatusMessage,
            last.IsSearching);

        var res = new MoveResponse(move.X, move.Y, (int)move.Player, dto);
        return JsonSerializer.Serialize(res, EngineProtocolJson.Default.MoveResponse);
    }

    public static (Move Move, SearchAnalysis? LastAnalysis) ReadResponse(string json, IReadOnlyList<Move> legalMoves)
    {
        MoveResponse res = JsonSerializer.Deserialize(json, EngineProtocolJson.Default.MoveResponse)!;
        PieceColor player = (PieceColor)res.MovePlayer;
        Move chosen = legalMoves.FirstOrDefault(m => m.X == res.MoveX && m.Y == res.MoveY && m.Player == player);
        if (chosen.Player == PieceColor.Empty && legalMoves.Count > 0)
        {
            chosen = legalMoves[0];
        }

        SearchAnalysis? last = res.LastAnalysis is null ? null : new SearchAnalysis(
            res.LastAnalysis.MoveNumber,
            res.LastAnalysis.Depth,
            res.LastAnalysis.Score,
            res.LastAnalysis.BestMoveX.HasValue && res.LastAnalysis.BestMoveY.HasValue && res.LastAnalysis.BestMovePlayer.HasValue
                ? new Move(res.LastAnalysis.BestMoveX.Value, res.LastAnalysis.BestMoveY.Value, (PieceColor)res.LastAnalysis.BestMovePlayer.Value)
                : null,
            res.LastAnalysis.Nodes,
            res.LastAnalysis.EvaluationsPerSec,
            TimeSpan.FromMilliseconds(res.LastAnalysis.ElapsedMs),
            res.LastAnalysis.StatusMessage,
            res.LastAnalysis.IsSearching);

        return (chosen, last);
    }
}


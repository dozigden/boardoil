using System.Text.Json.Serialization;

namespace BoardOil.Contracts.Board;

public sealed record ReorderBoardRequest(IReadOnlyList<BoardCardOrder?>? Columns);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoardCardOrder(int? ColumnId, IReadOnlyList<int>? CardIds);

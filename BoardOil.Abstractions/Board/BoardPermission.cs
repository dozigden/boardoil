namespace BoardOil.Abstractions.Board;

public enum BoardPermission
{
    BoardAccess = 0,
    BoardManageSettings = 1,
    BoardManageMembers = 2,
    ColumnManage = 3,
    CardCreate = 7,
    CardUpdate = 8,
    CardDelete = 9,
    CardMove = 10,
    TagManage = 11
}

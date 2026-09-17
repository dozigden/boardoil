namespace BoardOil.Mcp.Contracts;

public static class ToolDiscoveryOrder
{
    public const int IdentityGet = 100;
    public const int BoardList = 200;
    public const int BoardGet = 300;
    public const int CardSearch = 350;
    public const int CardGet = 400;
    public const int CardOptionsGet = 500;

    public const int CardCreate = 600;
    public const int CardUpdate = 700;
    public const int CardMove = 800;
    public const int CardCommentCreate = 900;
    public const int CardArchive = 1000;
    public const int CardRestore = 1050;
    public const int CardDelete = 1100;

    public const int CardAttachmentUpload = 1200;
    public const int CardAttachmentDownload = 1300;
    public const int CardAttachmentDelete = 1400;

    public const int TagCreate = 1500;
    public const int TagUpdate = 1600;
    public const int TagDelete = 1700;

    public const int SlickCreate = 1800;
    public const int SlickUpdate = 1900;
    public const int SlickDelete = 2000;
}

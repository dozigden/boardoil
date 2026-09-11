namespace BoardOil.Abstractions.Attachment;

public interface IAttachmentStorageService
{
    Stream Create(string storageKey);
    Stream OpenRead(string storageKey);
    void Delete(string storageKey);
}

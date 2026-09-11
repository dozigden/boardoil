using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Attachment;

public interface IAttachmentRepository : IRepositoryBase<EntityCardAttachment>;

public interface IAttachmentDownloadTicketRepository : IRepositoryBase<EntityAttachmentDownloadTicket>;

public interface IAttachmentUploadTicketRepository : IRepositoryBase<EntityAttachmentUploadTicket>;

public interface IAttachmentTransferAuditRepository : IRepositoryBase<EntityAttachmentTransferAudit>;

public interface ITemporaryBoardPackageRepository : IRepositoryBase<EntityTemporaryBoardPackage>;

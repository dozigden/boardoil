using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Ef.Repositories;

public sealed class AttachmentRepository(IAmbientDbContextLocator locator)
    : RepositoryBase<EntityCardAttachment>(locator), IAttachmentRepository;

public sealed class AttachmentDownloadTicketRepository(IAmbientDbContextLocator locator)
    : RepositoryBase<EntityAttachmentDownloadTicket>(locator), IAttachmentDownloadTicketRepository;

public sealed class AttachmentUploadTicketRepository(IAmbientDbContextLocator locator)
    : RepositoryBase<EntityAttachmentUploadTicket>(locator), IAttachmentUploadTicketRepository;

public sealed class AttachmentTransferAuditRepository(IAmbientDbContextLocator locator)
    : RepositoryBase<EntityAttachmentTransferAudit>(locator), IAttachmentTransferAuditRepository;

public sealed class TemporaryBoardPackageRepository(IAmbientDbContextLocator locator)
    : RepositoryBase<EntityTemporaryBoardPackage>(locator), ITemporaryBoardPackageRepository;

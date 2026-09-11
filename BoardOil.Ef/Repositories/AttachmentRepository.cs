using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Attachment;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Ef.Repositories;

public sealed class AttachmentRepository(IAmbientDbContextLocator locator)
    : RepositoryBase<EntityCardAttachment>(locator), IAttachmentRepository;

public sealed class TemporaryBoardPackageRepository(IAmbientDbContextLocator locator)
    : RepositoryBase<EntityTemporaryBoardPackage>(locator), ITemporaryBoardPackageRepository;

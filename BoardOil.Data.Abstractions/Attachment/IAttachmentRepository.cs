using BoardOil.Data.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Entities;

namespace BoardOil.Data.Abstractions.Attachment;

public interface IAttachmentRepository : IRepositoryBase<EntityCardAttachment>;

public interface ITemporaryBoardPackageRepository : IRepositoryBase<EntityTemporaryBoardPackage>;

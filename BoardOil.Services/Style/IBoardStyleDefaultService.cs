using BoardOil.Contracts.Style;

namespace BoardOil.Services.Style;

public interface IBoardStyleDefaultService
{
    Task<StyleDefaultDto> GetTagCreateDefaultStyleAsync(int boardId);
    Task<StyleDefaultDto> GetSlickCreateDefaultStyleAsync(int boardId);
    StyleDefaultDto BuildCreateDefaultStyle(IEnumerable<BoardStyleDefaultCandidate> existingStyles);
}

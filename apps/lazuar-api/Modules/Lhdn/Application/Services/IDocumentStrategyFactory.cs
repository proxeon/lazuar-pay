using Lazuar.ApiTypes;

namespace Modules.Lhdn.Application.Services;

public interface IDocumentStrategyFactory
{
    IUblDocumentStrategy GetStrategy(SubmitDocumentRequestDto request);
}

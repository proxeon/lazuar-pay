using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

public class StandardInvoiceStrategy : IUblDocumentStrategy
{
    private readonly ITemplateRendererService _rendererService;

    public StandardInvoiceStrategy(ITemplateRendererService rendererService)
    {
        _rendererService = rendererService;
    }

    public string Generate(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion)
    {
        var viewModel = ViewModelMapper.MapToViewModel(request, config, documentVersion);
        return _rendererService.Render("StandardInvoice.xml", viewModel);
    }
}

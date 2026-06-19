using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;

namespace Modules.Lhdn.Infrastructure.Services.Strategies;

public class ConsolidatedInvoiceStrategy : IUblDocumentStrategy
{
    private readonly ITemplateRendererService _rendererService;

    public ConsolidatedInvoiceStrategy(ITemplateRendererService rendererService)
    {
        _rendererService = rendererService;
    }

    public string Generate(SubmitDocumentRequestDto request, LhdnTenantConfig config, string documentVersion)
    {
        var viewModel = ViewModelMapper.MapToViewModel(request, config, documentVersion);
        return _rendererService.Render("ConsolidatedInvoice.xml", viewModel);
    }
}

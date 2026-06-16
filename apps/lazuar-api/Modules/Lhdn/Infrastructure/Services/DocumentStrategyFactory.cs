using System;
using Lazuar.ApiTypes;
using Microsoft.Extensions.DependencyInjection;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Infrastructure.Services;

public class DocumentStrategyFactory : IDocumentStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DocumentStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IUblDocumentStrategy GetStrategy(SubmitDocumentRequestDto request)
    {
        bool isB2c = string.IsNullOrWhiteSpace(request.Buyer_tin) || request.Buyer_tin == "EI00000000010";

        return request.Document_type switch
        {
            SubmitDocumentRequestDtoDocument_type._01 when isB2c => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("B2CConsolidatedInvoice"),
                
            SubmitDocumentRequestDtoDocument_type._01 => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("B2BStandardInvoice"),
                
            SubmitDocumentRequestDtoDocument_type._02 => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("CreditNote"),
                
            _ => throw new NotSupportedException($"Document type {request.Document_type} is not currently supported in this iteration.")
        };
    }
}

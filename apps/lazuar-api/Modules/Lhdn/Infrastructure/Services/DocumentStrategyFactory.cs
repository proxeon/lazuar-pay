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
                
            SubmitDocumentRequestDtoDocument_type._02 or 
            SubmitDocumentRequestDtoDocument_type._03 or 
            SubmitDocumentRequestDtoDocument_type._04 => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("CreditNote"),
                
            SubmitDocumentRequestDtoDocument_type._11 => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("SelfBilledInvoice"),
                
            SubmitDocumentRequestDtoDocument_type._12 or 
            SubmitDocumentRequestDtoDocument_type._13 or 
            SubmitDocumentRequestDtoDocument_type._14 => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("SelfBilledCredit"),
                
            _ => throw new NotSupportedException($"Document type {request.Document_type} is not currently supported in this iteration.")
        };
    }
}

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

        // Bypasses System.Text.Json enum deserialization corruption by extracting the raw parsed integer
        int rawEnumValue = (int)request.Document_type;
        string actualDocType = rawEnumValue.ToString("D2");

        return actualDocType switch
        {
            "01" when isB2c => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("B2CConsolidatedInvoice"),
                
            "01" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("B2BStandardInvoice"),
                
            "02" or "03" or "04" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("CreditNote"),
                
            "11" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("SelfBilledInvoice"),
                
            "12" or "13" or "14" => 
                _serviceProvider.GetRequiredKeyedService<IUblDocumentStrategy>("SelfBilledCredit"),
                
            _ => throw new NotSupportedException($"Document type {actualDocType} is not currently supported in this iteration.")
        };
    }
}

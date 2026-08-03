using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Application.Commands;

public record ValidateTaxpayerTinCommand(
    Guid OrganizationId,
    string Tin,
    string IdType,
    string IdValue) : IQuery<ValidateTinResponseDto>;

public class ValidateTaxpayerTinCommandHandler : IQueryHandler<ValidateTaxpayerTinCommand, ValidateTinResponseDto>
{
    private readonly ITaxpayerValidationService _validationService;

    public ValidateTaxpayerTinCommandHandler(ITaxpayerValidationService validationService)
    {
        _validationService = validationService;
    }

    public async Task<ValidateTinResponseDto> Handle(ValidateTaxpayerTinCommand request, CancellationToken ct)
    {
        var result = await _validationService.ValidateTinAsync(
            request.OrganizationId,
            request.Tin,
            request.IdType,
            request.IdValue,
            ct);

        return new ValidateTinResponseDto
        {
            Is_valid = result.IsValid,
            Tin = result.Tin,
            Taxpayer_name = result.TaxpayerName
        };
    }
}

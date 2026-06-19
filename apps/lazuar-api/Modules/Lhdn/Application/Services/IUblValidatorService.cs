namespace Modules.Lhdn.Application.Services;

public interface IUblValidatorService
{
    void Validate(string xmlString, string documentType);
}

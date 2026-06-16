namespace Modules.Lhdn.Application.Services;

public interface ITemplateRendererService
{
    string Render(string templateName, object model);
}

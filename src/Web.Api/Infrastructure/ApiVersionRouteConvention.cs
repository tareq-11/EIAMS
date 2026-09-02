using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Web.Api.Infrastructure;

internal sealed class ApiVersionRouteConvention(string prefix) : IApplicationModelConvention
{
    private readonly AttributeRouteModel prefixRoute = new(new RouteAttribute(prefix));

    public void Apply(ApplicationModel application)
    {
        foreach (ControllerModel controller in application.Controllers)
        {
            foreach (SelectorModel selector in controller.Selectors)
            {
                if (selector.AttributeRouteModel is null)
                {
                    continue;
                }

                selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(
                    prefixRoute,
                    selector.AttributeRouteModel);
            }
        }
    }
}

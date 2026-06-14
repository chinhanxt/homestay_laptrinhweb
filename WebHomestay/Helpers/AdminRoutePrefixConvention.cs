using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System;

namespace WebHomestay.Helpers
{
    public class AdminRoutePrefixConvention : IApplicationModelConvention
    {
        private readonly string _newPrefix;

        public AdminRoutePrefixConvention(string newPrefix)
        {
            _newPrefix = newPrefix;
        }

        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                foreach (var selector in controller.Selectors)
                {
                    if (selector.AttributeRouteModel != null)
                    {
                        var template = selector.AttributeRouteModel.Template;
                        if (template != null)
                        {
                            if (template.Equals("admin", StringComparison.OrdinalIgnoreCase))
                            {
                                selector.AttributeRouteModel.Template = _newPrefix;
                            }
                            else if (template.StartsWith("admin/", StringComparison.OrdinalIgnoreCase))
                            {
                                selector.AttributeRouteModel.Template = _newPrefix + template.Substring(5);
                            }
                        }
                    }
                }

                foreach (var action in controller.Actions)
                {
                    foreach (var selector in action.Selectors)
                    {
                        if (selector.AttributeRouteModel != null)
                        {
                            var template = selector.AttributeRouteModel.Template;
                            if (template != null)
                            {
                                if (template.Equals("admin", StringComparison.OrdinalIgnoreCase))
                                {
                                    selector.AttributeRouteModel.Template = _newPrefix;
                                }
                                else if (template.StartsWith("admin/", StringComparison.OrdinalIgnoreCase))
                                {
                                    selector.AttributeRouteModel.Template = _newPrefix + template.Substring(5);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

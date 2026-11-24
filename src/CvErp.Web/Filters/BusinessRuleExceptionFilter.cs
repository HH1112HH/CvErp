using CvErp.Infrastructure.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace CvErp.Web.Filters;

/// <summary>Chuyển lỗi nghiệp vụ thành thông báo thân thiện thay vì trang lỗi 500.</summary>
public class BusinessRuleExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not BusinessRuleException businessException)
        {
            return;
        }

        var tempDataFactory = context.HttpContext.RequestServices.GetService<ITempDataDictionaryFactory>();
        tempDataFactory?.GetTempData(context.HttpContext)["Error"] = businessException.Message;

        var referer = context.HttpContext.Request.Headers.Referer.ToString();
        var redirect = "/";
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, context.HttpContext.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            redirect = uri.PathAndQuery;
        }

        context.Result = new RedirectResult(redirect);
        context.ExceptionHandled = true;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SuperShortLink
{
    public class LoginAuthrizeAttribute : TypeFilterAttribute
    {
        public LoginAuthrizeAttribute() : base(typeof(LoginAuthrizeFilter))
        {

        }
    }

    public class LoginAuthrizeFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (IsAllowAnonymous(context))
            {
                return;
            }

            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
            {
                return;
            }

            context.HttpContext.Response.Redirect("/Login/Index");
            context.Result = new ObjectResult("无效凭证");
        }


        private bool IsAllowAnonymous(AuthorizationFilterContext context)
        {
            var hasAttribute = false;
            if (context.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
            {
                hasAttribute =
                    controllerActionDescriptor.MethodInfo.GetCustomAttributes(inherit: true).Any(a => a.GetType() == typeof(AllowAnonymousAttribute))
                    || controllerActionDescriptor.ControllerTypeInfo.GetCustomAttributes(inherit: true).Any(a => a.GetType() == typeof(AllowAnonymousAttribute));
            }
            return hasAttribute;
        }
    }
}

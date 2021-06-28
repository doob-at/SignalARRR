using System.Threading.Tasks;
using doob.SignalARRR.Server.ExtensionMethods;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace doob.SignalARRR.Server {
    public class SignalARRRAccessTokenValidationMiddleware {
        private readonly RequestDelegate _next;

        public SignalARRRAccessTokenValidationMiddleware(RequestDelegate next) {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext) {

            var isSignalRHub = httpContext.GetEndpoint().IsSignalREndpoint();

            var accessToken = httpContext.Request.Query["access_token"];
            if (isSignalRHub && !string.IsNullOrEmpty(accessToken)) {
                httpContext.Request.Headers["Authorization"] = $"Bearer {accessToken}";
            }

            await _next(httpContext);
        }
    }

    public static class SignalARRRAccessTokenValidationMiddlewareExtensions {

        public static IApplicationBuilder UseSignalARRRAccessTokenValidation(this IApplicationBuilder appBuilder) {
            appBuilder.UseMiddleware<SignalARRRAccessTokenValidationMiddleware>();
            return appBuilder;
        }

    }
}

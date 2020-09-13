using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SignalARRR.Server.ExtensionMethods;

namespace SignalARRR.Server {
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

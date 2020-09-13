using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace SignalARRR.Server.ExtensionMethods {
    public static class HubEndpointConventionBuilderExtensions {

        public static HubEndpointConventionBuilder MapHubWithResponseController<THub>(
            this IEndpointRouteBuilder endpoints, string pattern) where THub : HARRR {

            var ret = endpoints.MapHub<THub>(pattern);
            endpoints.MapPost($"{pattern}/response", async context => {
                var requestManager = context.RequestServices.GetRequiredService<ServerRequestManager>();
                var json = await context.GetRawBodyStringAsync(Encoding.UTF8);
                var msg = Converter.Json.ToObject<ClientResponseMessage>(json);
                requestManager.CompleteRequest(msg);
                await context.Ok();
            });
            return ret;

        }

        public static HubEndpointConventionBuilder MapHubWithResponseController<THub>(
            this IEndpointRouteBuilder endpoints, string pattern,
            Action<HttpConnectionDispatcherOptions> configureOptions) where THub : HARRR {

            var ret = endpoints.MapHub<THub>(pattern, configureOptions);
            endpoints.MapPost($"{pattern}/response", async context => {
                var requestManager = context.RequestServices.GetRequiredService<ServerRequestManager>();
                var json = await context.GetRawBodyStringAsync(Encoding.UTF8);
                var msg = Converter.Json.ToObject<ClientResponseMessage>(json);
                requestManager.CompleteRequest(msg);
                await context.Ok();
            });
            return ret;

        }
        
        //public static TBuilder WithResponseController<TBuilder>(this TBuilder builder, string pattern = null) where TBuilder : IEndpointConventionBuilder {
        //    if (builder == null) {
        //        throw new ArgumentNullException(nameof(builder));
        //    }
            
        //    builder.Add(endpointBuilder => {
                
        //        endpointBuilder.Metadata.Add(new HostAttribute(hosts));
        //    });
        //    return builder;
        //}
    }
}

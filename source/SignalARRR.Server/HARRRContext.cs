using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reflectensions;
using Reflectensions.ExtensionMethods;
using SignalARRR.Constants;

namespace SignalARRR.Server {
    internal class ClientContextDispatcher<T> : IClientContextDispatcher where T : HARRR {


        private IHubContext<T> HubContext { get; }

        private ServerRequestManager ServerRequestManager { get; }


        public ClientContextDispatcher(IHubContext<T> hubContext, ServerRequestManager serverRequestManager) {
            HubContext = hubContext;
            ServerRequestManager = serverRequestManager;
        }



        public Task<TResult> InvokeClientAsync<TResult>(string clientId, ServerRequestMessage serverRequestMessage, CancellationToken cancellationToken) {


            return InvokeClientMessageAsync<TResult>(clientId, MethodNames.InvokeServerRequest, serverRequestMessage, cancellationToken);

        }

        public Task ProxyClientAsync(string clientId, ServerRequestMessage serverRequestMessage, HttpContext httpContext) {


            return ProxyClientMessageAsync(clientId, MethodNames.InvokeServerRequest, serverRequestMessage, httpContext);

        }

        public Task SendClientAsync(string clientId, ServerRequestMessage serverRequestMessage, CancellationToken cancellationToken) {
            return SendClientMessageAsync(clientId, MethodNames.InvokeServerMessage, serverRequestMessage, cancellationToken);
        }

        public async Task<string> Challenge(string clientId) {

            try {
                var msg = new ServerRequestMessage(MethodNames.ChallengeAuthentication);
                var ct = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                return await InvokeClientMessageAsync<string>(clientId, MethodNames.ChallengeAuthentication, msg, ct.Token);
            } catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
            
        }

        private async Task<TResult> InvokeClientMessageAsync<TResult>(string clientId, string methodName, ServerRequestMessage serverRequestMessage, CancellationToken cancellationToken) {


            var m = ServerRequestManager.AddRequest(serverRequestMessage.Id);
            await HubContext.Clients.Client(clientId).SendCoreAsync(methodName, new[] { serverRequestMessage }, cancellationToken);

            
            await Task.Run(() => {
                try {
                    Task.WaitAny(new Task[] { m.Task }, cancellationToken);
                } catch (Exception) {
                    ServerRequestManager.CancelRequest(serverRequestMessage.Id);
                    throw;
                }
            });


            var jToken = await m.Task;
           
            return Json.Converter.ToObject<TResult>(jToken);

        }

        private async Task ProxyClientMessageAsync(string clientId, string methodName, ServerRequestMessage serverRequestMessage, HttpContext httpContext) {


            var m = ServerRequestManager.AddProxyRequest(serverRequestMessage.Id, httpContext);
            await HubContext.Clients.Client(clientId).SendCoreAsync(methodName, new[] { serverRequestMessage }, httpContext.RequestAborted);


            await Task.Run(() => {
                try {
                    Task.WaitAny(new Task[] { m.Task }, httpContext.RequestAborted);
                } catch (Exception) {
                    ServerRequestManager.CancelRequest(serverRequestMessage.Id);
                    throw;
                }
            });

            //await m.Task;
        }


        //private TResult Deserialize<TResult>(Stream s) {
        //    using (StreamReader reader = new StreamReader(s))
        //    using (JsonTextReader jsonReader = new JsonTextReader(reader)) {
        //        JsonSerializer ser = new JsonSerializer();
        //        return ser.Deserialize<TResult>(jsonReader);
        //    }
        //}

        private async Task SendClientMessageAsync(string clientId, string methodName, ServerRequestMessage serverRequestMessage, CancellationToken cancellationToken) {


            var m = ServerRequestManager.AddRequest(serverRequestMessage.Id);
            await HubContext.Clients.Client(clientId).SendCoreAsync(methodName, new[] { serverRequestMessage }, cancellationToken);
            
        }


    }


}

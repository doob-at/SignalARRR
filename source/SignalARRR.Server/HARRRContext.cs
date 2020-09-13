using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
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


            var res = await m.Task;

            if (!String.IsNullOrWhiteSpace(res.ErrorMessage)) {
                throw new Exception(res.ErrorMessage);
            }

            if (res.PayLoad is JObject jObject) {
                return Converter.Json.ToObject<TResult>(jObject);
            }

            if (res.PayLoad.TryTo<TResult>(out var value)) {
                return value;
            }

            
            return default;

        }


        
    }

   
}

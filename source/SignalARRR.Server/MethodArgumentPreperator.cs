using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using SignalARRR.RemoteReferenceTypes;
using SignalARRR.Server.ExtensionMethods;

namespace SignalARRR.Server {
    public class MethodArgumentPreperator {


        private readonly ClientContext _clientContext;
        private readonly ServerPushStreamManager _pushStreamManager;

        public MethodArgumentPreperator(ClientContext clientContext) {
            _clientContext = clientContext;
            _pushStreamManager = clientContext.ServiceProvider.GetRequiredService<ServerPushStreamManager>();
        }

        internal IEnumerable<object> PrepareArguments(IEnumerable<object> arguments) {
            foreach (var argument in arguments) {

                switch (argument) {
                    case null: {
                            yield return null;
                            continue;
                        }
                    case Stream stream: {
                        yield return PrepareStream(stream);
                            continue;
                        }
                    default:
                        yield return argument;
                        break;
                }
            }
        }

        private StreamReference PrepareStream(Stream stream) {
            var identifier = _pushStreamManager.StoreStreamForDownload(stream, _clientContext.ConnectedTo);
            return new StreamReference() { Uri = identifier };
        }

    }
}

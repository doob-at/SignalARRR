using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using doob.Reflectensions.Helper;
using SignalARRR.CodeGenerator;

namespace SignalARRR.Client {
    public class ClientClassCreatorHelper : ClassCreatorHelper {
        private readonly HARRRConnection _harrrConnection;


        public ClientClassCreatorHelper(HARRRConnection harrrConnection) {
            _harrrConnection = harrrConnection;
        }

        public override T Invoke<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, arguments);
            msg.GenericArguments = genericArguments.ToArray();
            return SimpleAsyncHelper.RunSync(() => _harrrConnection.InvokeCoreAsync<T>(msg, cancellationToken));
        }

        public override Task<T> InvokeAsync<T>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, arguments);
            msg.GenericArguments = genericArguments.ToArray();
            return _harrrConnection.InvokeCoreAsync<T>(msg, cancellationToken);
        }

        public override void Send(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, arguments);
            msg.GenericArguments = genericArguments.ToArray();
            SimpleAsyncHelper.RunSync(() => _harrrConnection.SendCoreAsync(msg, cancellationToken));
        }

        public override Task SendAsync(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, arguments);
            msg.GenericArguments = genericArguments.ToArray();
            return _harrrConnection.SendCoreAsync(msg, cancellationToken);
        }

        public override IAsyncEnumerable<TResult> StreamAsync<TResult>(string methodName, IEnumerable<object> arguments, string[] genericArguments, CancellationToken cancellationToken = default) {
            var msg = new ClientRequestMessage(methodName, arguments);
            msg.GenericArguments = genericArguments.ToArray();
            return _harrrConnection.StreamAsyncCore<TResult>(msg, cancellationToken);
        }
    }
}

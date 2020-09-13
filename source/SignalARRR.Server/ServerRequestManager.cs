using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SignalARRR.Server {
    public class ServerRequestManager {


        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ClientResponseMessage>> _pendingMethodCalls = new ConcurrentDictionary<Guid, TaskCompletionSource<ClientResponseMessage>>();


        //public TaskCompletionSource<ClientResponseMessage> AddorGetRequest(Guid id) {

        //    return _pendingMethodCalls.GetOrAdd(id, guid => new TaskCompletionSource<ClientResponseMessage>());
            
        //}

        public TaskCompletionSource<ClientResponseMessage> AddRequest(Guid id) {

            var methodCallCompletionSource = new TaskCompletionSource<ClientResponseMessage>();
            if (_pendingMethodCalls.TryAdd(id, methodCallCompletionSource)) {
                return methodCallCompletionSource;
            }

            throw new Exception("Can't add Request to Manager");
        }


        public void CompleteRequest(ClientResponseMessage clientResponseMessage) {

            if (_pendingMethodCalls.TryRemove(clientResponseMessage.Id, out var methodCallCompletionSource)) {
                methodCallCompletionSource.SetResult(clientResponseMessage);
            }
        }

        public void CancelRequest(Guid id) {
            if (_pendingMethodCalls.TryRemove(id, out var methodCallCompletionSource)) {
                methodCallCompletionSource.SetCanceled();
            }
        }

    }
}

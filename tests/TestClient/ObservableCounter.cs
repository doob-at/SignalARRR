using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SignalARRR;

namespace TestClient {
    public class ObservableCounter {

        private CancellationTokenSource cancellationTokenSource;
        public HARRRConnection Connection { get; }

        public ObservableCounter(HARRRConnection connection) {
            Connection = connection;
        }

       

        public async Task StartAsync() {
            cancellationTokenSource = new CancellationTokenSource();

            try {
                var stream = Connection.StreamAsync<string>(
                "Test1.ObservableCounter", 10, 500, cancellationTokenSource.Token);

                await foreach (var count in stream) {
                    Console.WriteLine($"{count}");
                }

                Console.WriteLine("Finished ObservableCounter");
            } catch (OperationCanceledException) {
                Console.WriteLine("Finished ObservableCounter");
            } 
        }

        public void Stop() {
            cancellationTokenSource.Cancel();
        }
    }
}

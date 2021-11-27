using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using doob.SignalARRR.Server;
using SignalARRR.Tests.SharedModels;

namespace SignalARRR.Tests {
    public class TestServerMethods : ServerMethods<TestHub>, ITestServerMethods {
        public string GetName() {
            return "MyName";
        }

        public Task<string> GetNameAsync() {
            return Task.FromResult("MyNameAsync");
        }

        public Guid GetGuid() {
            return Guid.NewGuid();
        }

        public Task<Guid> GetGuidAsync() {
            return Task.FromResult(Guid.NewGuid());
        }

        public void Nothing() {
            
        }

        public Task NothingAsync() {
            return Task.CompletedTask;
        }

        public string SameName() {
            return "OK";
        }

        public bool SameName(bool value) {
            return value;
        }

        public bool SameName(bool value, int i) {
            return value;
        }
    }
}

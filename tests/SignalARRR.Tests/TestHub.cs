using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using doob.SignalARRR.Server;
using SignalARRR.Tests.SharedModels;

namespace SignalARRR.Tests {
    
    public partial class TestHub : HARRR {

        
        public TestHub(IServiceProvider serviceProvider) : base(serviceProvider) {

            
        }


        //public string GetName() {
        //    return "MyName";
        //}

        //public Task<string> GetNameAsync() {
        //    return Task.FromResult("MyNameAsync");
        //}

        //public Guid GetGuid() {
        //    return Guid.NewGuid();
        //}

        //public Task<Guid> GetGuidAsync() {
        //    return Task.FromResult(Guid.NewGuid());
        //}

        //public void Nothing() {
            
        //}

        //public Task NothingAsync() {
        //    return Task.CompletedTask;
        //}

        //public string SameName() {
        //    return "OK";
        //}

        //public bool SameName(bool value) {
        //    return value;
        //}

        //public bool SameName(bool value, int i) {
        //    return value;
        //}
    }
}

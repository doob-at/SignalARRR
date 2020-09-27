using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SignalARRR;
using SignalARRR.Attributes;
using SignalARRR.Client;
using TestShared;

namespace TestClient {

    [MessageName("ClientTest")]
    public class TestClientMethods: ITestClientMethods {


        public Task<DateTime> GetDate() {

            var dt = DateTime.Now;
            return Task.FromResult(dt);
        }

        public Task<Dictionary<string, object>> GetDictionary(DateTime dt) {
            
            var dict = new Dictionary<string, object>();
            dict.Add("Date", dt);
            dict.Add("Name", GetName());




            return Task.FromResult(dict);
        }

        public string GetName() {
            return $"{Environment.MachineName}\\{Environment.UserName}";
        }
    }
}

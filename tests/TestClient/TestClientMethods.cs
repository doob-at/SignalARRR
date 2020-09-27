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

        public Task<T> InvokeAsync<T>(string command, Dictionary<string, object> variables = null) {
            Console.WriteLine(typeof(T));
            return default;
        }

        public T Invoke<T>(string command, Dictionary<string, object> variables = null) {
            Console.WriteLine(typeof(T));
            return default;
        }

        public string GetName() {
            return $"{Environment.MachineName}\\{Environment.UserName}";
        }
    }
}

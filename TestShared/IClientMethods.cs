using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TestShared {
    public interface ITestClientMethods {

        //string GetName();

        //Task<DateTime> GetDate();

        //Task<Dictionary<string, object>> GetDictionary(DateTime dt);

        //Task<T> InvokeAsync<T>(string command, Dictionary<string, object> variables = null);
        T Invoke<T>(string command, Dictionary<string, object> variables = null);

        void Nix();

        List<string> GetContent(int count);

        bool CreateObject(string className, Dictionary<string, object> properties);

        bool CreateObjectFromTemplate(string templateName, Dictionary<string, object> properties);
    }
}

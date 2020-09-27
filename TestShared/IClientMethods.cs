using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TestShared {
    public interface ITestClientMethods {

        string GetName();

        Task<DateTime> GetDate();

        Task<Dictionary<string, object>> GetDictionary(DateTime dt);
    }
}

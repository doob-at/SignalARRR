using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Converters;
using Reflectensions.JsonConverters;

namespace SignalARRR.Client {
    public class JsonConvert {

        private readonly Lazy<Reflectensions.Json> lazyJson = new Lazy<Reflectensions.Json>(() => new Reflectensions.Json()
            .RegisterJsonConverter<StringEnumConverter>()
            .RegisterJsonConverter<DefaultDictionaryConverter>()
        );

        private Reflectensions.Json Convert => lazyJson.Value;


    }
}

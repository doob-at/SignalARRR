using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Converters;
using Reflectensions.JsonConverters;

namespace SignalARRR.Server {
    internal class Converter {

        private static readonly Lazy<Reflectensions.Json> lazyJson = new Lazy<Reflectensions.Json>(() => new Reflectensions.Json()
            .RegisterJsonConverter<StringEnumConverter>()
            .RegisterJsonConverter<DefaultDictionaryConverter>()
        );

        public static Reflectensions.Json Json => lazyJson.Value;

    }
}

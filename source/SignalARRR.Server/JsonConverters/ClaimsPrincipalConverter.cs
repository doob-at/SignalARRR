using System;
using System.Security.Claims;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace doob.SignalARRR.Server.JsonConverters {
    public class ClaimsPrincipalConverter : JsonConverter {
       

        public override bool CanConvert(Type objectType) {
            return (objectType == typeof(ClaimsPrincipal));
        }

        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            var claim = (ClaimsPrincipal)value;
            JObject jo = new JObject();
            jo.Add("Name", claim?.Identity?.Name);
            jo.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            throw new NotImplementedException();
        }

    }

}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Reflectensions;
using SignalARRR.Client.ExtensionMethods;

namespace SignalARRR.Client {
    public class StreamReferenceResolver {

        private readonly StreamReference _streamReference;
        private readonly HARRRContext _harrrContext;

        public StreamReferenceResolver(StreamReference streamReference, HARRRContext harrrContext) {
            _streamReference = streamReference;
            _harrrContext = harrrContext;
        }


        public async Task<Stream> ProcessStreamArgument() {

            var uri = new Uri(_streamReference.Uri);
            switch (uri.Scheme.ToLower()) {

                case "http":
                case "https": {
                    return await DownloadStream(uri);
                    
                }
                default: {
                    throw new Exception($"StreamReference.Scheme '{uri.Scheme}' is not implemented!");
                }
            }
        }


        private async Task<Stream> DownloadStream(Uri uri) {
            var httpClient = new HttpClient();
            var res = await httpClient.GetAsync(uri);
            return await res.Content.ReadAsStreamAsync();

        }

    }
}

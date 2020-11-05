﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SignalARRR.Server.ExtensionMethods {
    public static class HttpContextExtensions {

        public static Task<string> GetRawBodyStringAsync(this HttpContext httpContext, Encoding encoding) {

            if (httpContext.Request.ContentLength == null || !(httpContext.Request.ContentLength > 0))
                return Task.FromResult<string>(null);

            using var reader = new StreamReader(httpContext.Request.Body, encoding, true, 1024, true);
            return reader.ReadToEndAsync();

        }

        public static void ProxyFromHARRRClient<TInterface>(this HttpContext httpContext, ClientContext clientContext,
            Action<TInterface> action) {

            clientContext.ProxyToHttpContext(httpContext, action);

        }


    }
}

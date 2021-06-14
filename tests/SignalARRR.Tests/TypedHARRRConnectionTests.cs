using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using SignalARRR.Client;
using SignalARRR.Tests.SharedModels;
using Xunit;

namespace SignalARRR.Tests
{
    [Collection("Simple")]
    public class TypedHARRRConnectionTests {

        SignalARRRServerInstanceFixture fixture;
        HARRRConnection harrrConnection;


        public TypedHARRRConnectionTests(SignalARRRServerInstanceFixture fixture) {
            this.fixture = fixture;


            var testServer = this.fixture.GetHost().GetTestServer();

            harrrConnection = HARRRConnection.Create(builder => {
                builder.WithUrl($"{testServer.BaseAddress}signalr/testhub", options => {
                    options.HttpMessageHandlerFactory = _ => testServer.CreateHandler();
                });
            });
            
        }

        private async Task<T> GetTypeConnection<T>() {
            await harrrConnection.StartAsync();
            return harrrConnection.GetTypedMethods<T>();
        }

        [Fact]
        public async Task GetString() {


            var serverMethods = await GetTypeConnection<ITestServerMethods>();
            var name = serverMethods.GetName();

            Assert.Equal("MyName", name);
        }

        [Fact]
        public async Task GetStringAsync() {

            var serverMethods = await GetTypeConnection<ITestServerMethods>();
            var name = await serverMethods.GetNameAsync();

            Assert.Equal("MyNameAsync", name);
        }
    }
}

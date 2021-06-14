using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using SignalARRR.Client;
using Xunit;

namespace SignalARRR.Tests
{
    [Collection("Simple")]
    public class SimpleHARRRConnectionTests {

        SignalARRRServerInstanceFixture fixture;
        HARRRConnection harrrConnection;


        public SimpleHARRRConnectionTests(SignalARRRServerInstanceFixture fixture) {
            this.fixture = fixture;


            var testServer = this.fixture.GetHost().GetTestServer();

            harrrConnection = HARRRConnection.Create(builder => {
                builder.WithUrl($"{testServer.BaseAddress}signalr/testhub", options => {
                    options.HttpMessageHandlerFactory = _ => testServer.CreateHandler();
                });
            });
            
        }

        [Fact]
        public async Task GetString() {

            await harrrConnection.StartAsync();

            var name = await harrrConnection.InvokeAsync<string>("GetName");

            Assert.Equal("MyName", name);
        }

        [Fact]
        public async Task GetStringAsync() {

            await harrrConnection.StartAsync();

            var name = await harrrConnection.InvokeAsync<string>("GetNameAsync");

            Assert.Equal("MyNameAsync", name);
        }
    }
}

using System;
using System.Collections;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiBackendFallbackTests
    {
        private static Task<byte[]> Send(string url, CancellationToken token) =>
            (Task<byte[]>)typeof(YuiBackendClient).GetMethod("SendHttpClientBytesAsync",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                new object[] { HttpMethod.Get, url, null, 5, "application/json", token });

        [UnityTest]
        public IEnumerator FallbackPreservesBody() => Await(Task.Run(() => CheckResponse(200)));

        [UnityTest]
        public IEnumerator FallbackPreservesHttpFailure() => Await(Task.Run(() => CheckResponse(401)));

        [UnityTest]
        public IEnumerator InFlightCancellationStaysCancellation() => Await(Task.Run(CheckCancellation));

        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
        }

        private static async Task CheckResponse(int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
                var server = Task.Run(async () =>
                {
                    using var peer = await listener.AcceptTcpClientAsync();
                    var stream = peer.GetStream();
                    await stream.ReadAsync(new byte[4096], 0, 4096);
                    const string body = "{\"status\":\"test\"}";
                    var bytes = Encoding.UTF8.GetBytes("HTTP/1.1 " + status + " Test\r\nContent-Length: "
                        + body.Length + "\r\nConnection: close\r\n\r\n" + body);
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                });
                if (status == 200)
                    Assert.AreEqual("{\"status\":\"test\"}", Encoding.UTF8.GetString(await Send(url, CancellationToken.None)));
                else
                {
                    try { await Send(url, CancellationToken.None); Assert.Fail("HTTP failure was swallowed."); }
                    catch (YuiBackendException error) { Assert.AreEqual(401, error.StatusCode); }
                }
                await server;
            }
            finally { listener.Stop(); }
        }

        private static async Task CheckCancellation()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                var send = Send("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port, cancel.Token);
                using var peer = await listener.AcceptTcpClientAsync();
                cancel.Cancel();
                try { await send; Assert.Fail("Cancelled request completed successfully."); }
                catch (OperationCanceledException) { }
                Assert.IsTrue(send.IsCanceled, "Cancellation must not become a connection error and trigger recovery.");
            }
            finally { listener.Stop(); }
        }
    }
}

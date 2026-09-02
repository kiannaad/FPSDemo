using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CGame.Network
{
    public sealed class DedicatedServerHealthServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private DedicatedServerHealthSnapshot snapshot;
        private Task loopTask;

        public Func<DedicatedFireQueryRequest, Task<DedicatedFireQueryResult>> FireQueryAsync { get; set; }

        public void Start(int port, DedicatedServerHealthSnapshot initialSnapshot)
        {
            snapshot = initialSnapshot ?? throw new ArgumentNullException(nameof(initialSnapshot));
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            loopTask = RunAsync(cancellation.Token);
        }

        public void Publish(DedicatedServerHealthSnapshot nextSnapshot)
        {
            Interlocked.Exchange(ref snapshot, nextSnapshot);
        }

        public void Dispose()
        {
            cancellation.Cancel();
            listener.Close();
            try { loopTask?.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
            catch (HttpListenerException) { }
            catch (ObjectDisposedException) { }
            cancellation.Dispose();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                if (string.Equals(context.Request.Url?.AbsolutePath, "/fire-query", StringComparison.Ordinal))
                {
                    await HandleFireQueryAsync(context, cancellationToken).ConfigureAwait(false);
                    continue;
                }
                if (!string.Equals(context.Request.Url?.AbsolutePath, "/health", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    continue;
                }

                byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot));
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = payload.Length;
                await context.Response.OutputStream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
                context.Response.Close();
            }
        }

        private async Task HandleFireQueryAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) ||
                FireQueryAsync == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.Close();
                return;
            }

            using var reader = new System.IO.StreamReader(context.Request.InputStream, Encoding.UTF8);
            string body = await reader.ReadToEndAsync().ConfigureAwait(false);
            DedicatedFireQueryRequest request = JsonUtility.FromJson<DedicatedFireQueryRequest>(body);
            DedicatedFireQueryResult result = request == null
                ? new DedicatedFireQueryResult { Accepted = false, Failure = "InvalidRequest" }
                : await FireQueryAsync(request).ConfigureAwait(false);
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(result));
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = payload.Length;
            await context.Response.OutputStream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
            context.Response.Close();
        }
    }
}

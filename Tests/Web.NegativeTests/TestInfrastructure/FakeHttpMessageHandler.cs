using System.Net;

namespace Web.NegativeTests.TestInfrastructure
{
    /// <summary>
    /// Stands in for the network when testing external HTTP services: instead of hitting a real API,
    /// HttpClient is pointed at this handler so tests can simulate
    /// "the service is unreachable" (an exception) or "the service returned an error" (a non-2xx
    /// response) deterministically, without any real HTTP call ever leaving the machine.
    /// </summary>
    public sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        private FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = (request, _) => Task.FromResult(responder(request));
        }

        private FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        /// <summary>Every request gets this fixed status code and body back.</summary>
        public static FakeHttpMessageHandler ReturningStatus(HttpStatusCode statusCode, string body = "") =>
            new(_ => new HttpResponseMessage(statusCode) { Content = new StringContent(body) });

        /// <summary>Every request gets this fixed JSON body back with a 200 OK.</summary>
        public static FakeHttpMessageHandler ReturningJson(string json) =>
            new(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });

        public static FakeHttpMessageHandler Responding(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            new(responder);

        public static FakeHttpMessageHandler RespondingAsync(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) =>
            new(responder);

        /// <summary>Simulates the service being completely unreachable - DNS failure, connection refused, timeout, etc.</summary>
        public static FakeHttpMessageHandler ThrowingConnectionFailure() =>
            new(_ => throw new HttpRequestException("Simulated failure: the service could not be reached."));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _responder(request, cancellationToken);

        public static HttpClient BuildClient(FakeHttpMessageHandler handler, Uri baseAddress) =>
            new(handler) { BaseAddress = baseAddress };
    }
}

using System.Globalization;
using System.Net;
using System.Text.Json;
using StopwatchOverlay.Habitica;

static class BatchTransportChecks
{
    public static async Task Run()
    {
        var credentials = new HabiticaCredentials("11111111-1111-4111-8111-111111111111", "fake", "22222222-2222-4222-8222-222222222222-Stopwatch");
        var h = new Handler(); using var api = new HabiticaApiClient(new HttpClient(h));
        h.Json = "{\"success\":true,\"data\":[{\"_id\":\"a\",\"alias\":\"one\",\"text\":\"One\",\"completed\":false,\"checklist\":[]},{\"_id\":\"b\",\"alias\":\"two\",\"text\":\"Two\",\"completed\":false,\"checklist\":[]}]}";
        var payload = new[] {new HabiticaTaskPayload("One", "", null, "one"),new HabiticaTaskPayload("Two", "", null, "two")};
        var result = await api.CreateTasksAsync(credentials, payload);
        Require(result.Success && result.Data?.Count == 2 && h.Uri == "https://habitica.com/api/v3/tasks/user", "Bulk create response was not parsed.");
        using (var body = JsonDocument.Parse(h.Body)) Require(body.RootElement.ValueKind == JsonValueKind.Array && body.RootElement.GetArrayLength() == 2, "Bulk request is not an array.");
        int calls = h.Calls;
        Require(!(await api.CreateTasksAsync(credentials, Enumerable.Repeat(payload[0], 21).ToArray())).Success && h.Calls == calls, "Oversized create batch was transmitted.");
        h.Json = "{\"success\":true,\"data\":{\"_id\":\"a\",\"alias\":\"one\",\"text\":\"One\",\"completed\":false,\"checklist\":[]}}";
        Require((await api.CreateTasksAsync(credentials, payload.Take(1).ToArray())).Data?.Count == 1, "Single-task object response unsupported.");
        h.Code = HttpStatusCode.BadRequest;
        Require((await api.CreateTasksAsync(credentials, payload)).OutcomeUnknown, "Potential partial bulk failure lost reconciliation guard.");
        h.Code = HttpStatusCode.OK; h.Headers["X-RateLimit-Remaining"] = "0";
        h.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddSeconds(12).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var epoch = await api.GetTaskAsync(credentials, "a");
        Require(epoch.RetryAfter > TimeSpan.FromSeconds(10) && epoch.RetryAfter < TimeSpan.FromSeconds(16), "Known short reset was inflated to a minute.");
        h.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddSeconds(15).ToString("ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'", CultureInfo.InvariantCulture);
        var jsDate = await api.GetTaskAsync(credentials, "a");
        Require(jsDate.RetryAfter > TimeSpan.FromSeconds(12) && jsDate.RetryAfter < TimeSpan.FromSeconds(19), "Habitica's JavaScript date reset was ignored.");
        h.Headers.Remove("X-RateLimit-Reset"); h.Headers["Retry-After"] = "12.4";
        Require((await api.GetTaskAsync(credentials, "a")).RetryAfter == TimeSpan.FromSeconds(13), "Fractional Retry-After not rounded safely.");
        h.Headers["Retry-After"] = "120"; h.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddSeconds(12).ToUnixTimeSeconds().ToString();
        Require((await api.GetTaskAsync(credentials, "a")).RetryAfter == TimeSpan.FromSeconds(120), "Long server deadline was shortened.");
        h.Headers["Retry-After"] = "invalid"; h.Headers["X-RateLimit-Reset"] = "invalid";
        Require((await api.GetTaskAsync(credentials, "a")).RetryAfter == TimeSpan.FromSeconds(60), "Missing reset hints lost conservative fallback.");
        Console.WriteLine("PASS bulk HTTP payloads, uncertain partial result, fractional Retry-After and exact reset dates");
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class Handler : HttpMessageHandler
    {
        public string Json = "", Body = "", Uri = ""; public int Calls; public HttpStatusCode Code = HttpStatusCode.OK;
        public Dictionary<string,string> Headers = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Uri = request.RequestUri!.AbsoluteUri; Body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            var response = new HttpResponseMessage(Code) { Content = new StringContent(Json) };
            foreach (var h in Headers) response.Headers.TryAddWithoutValidation(h.Key,h.Value);
            return response;
        }
    }
}

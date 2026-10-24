using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace StopwatchOverlay.Habitica;

/// <summary>Single-attempt transport. Scheduling and uncertain-outcome reconciliation belong to the worker.</summary>
public sealed class HabiticaApiClient : IDisposable
{
    private readonly HttpClient http;
    private readonly bool ownsHttp;
    public HabiticaApiClient(HttpClient? http = null)
    {
        ownsHttp = http is null;
        this.http = http ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(25) };
    }
    public void Dispose() { if (ownsHttp) http.Dispose(); }
    private static string Id(string value) => Uri.EscapeDataString(value ?? "");
    private static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 250 && !value.Any(char.IsControl);
    public Task<HabiticaApiResult<HabiticaRemoteTask>> GetTaskAsync(HabiticaCredentials c, string id, CancellationToken ct = default) => TaskRequest(c,HttpMethod.Get,$"tasks/{Id(id)}",null,ValidId(id),ct);
    public Task<HabiticaApiResult<HabiticaRemoteTask>> CreateTaskAsync(HabiticaCredentials c, HabiticaTaskPayload payload, CancellationToken ct = default) => TaskRequest(c,HttpMethod.Post,"tasks/user",Payload(payload,true),!string.IsNullOrWhiteSpace(payload.Text),ct);
    public Task<HabiticaApiResult<HabiticaRemoteTask>> UpdateTaskAsync(HabiticaCredentials c,string id,HabiticaTaskPayload payload,CancellationToken ct = default) => TaskRequest(c,HttpMethod.Put,$"tasks/{Id(id)}",Payload(payload,false),ValidId(id)&&!string.IsNullOrWhiteSpace(payload.Text),ct);
    public Task<HabiticaApiResult<HabiticaRemoteTask>> AddChecklistItemAsync(HabiticaCredentials c,string taskId,string text,bool completed,CancellationToken ct=default) => TaskRequest(c,HttpMethod.Post,$"tasks/{Id(taskId)}/checklist",new {text,completed},ValidId(taskId)&&!string.IsNullOrWhiteSpace(text),ct);
    public Task<HabiticaApiResult<HabiticaRemoteTask>> UpdateChecklistItemAsync(HabiticaCredentials c,string taskId,string itemId,string text,bool completed,CancellationToken ct=default) => TaskRequest(c,HttpMethod.Put,$"tasks/{Id(taskId)}/checklist/{Id(itemId)}",new {text,completed},ValidId(taskId)&&ValidId(itemId)&&!string.IsNullOrWhiteSpace(text),ct);
    public Task<HabiticaApiResult<HabiticaRemoteTask>> DeleteChecklistItemAsync(HabiticaCredentials c,string taskId,string itemId,CancellationToken ct=default) => TaskRequest(c,HttpMethod.Delete,$"tasks/{Id(taskId)}/checklist/{Id(itemId)}",null,ValidId(taskId)&&ValidId(itemId),ct);
    public Task<HabiticaApiResult<bool>> DeleteTaskAsync(HabiticaCredentials c,string id,CancellationToken ct=default) => Send(c,HttpMethod.Delete,$"tasks/{Id(id)}",null,ValidId(id), d => d.ValueKind is JsonValueKind.Object or JsonValueKind.Null ? true : throw new JsonException(),ct);
    public Task<HabiticaApiResult<bool>> SetCompletedAsync(HabiticaCredentials c,string id,bool completed,CancellationToken ct=default) => Send(c,HttpMethod.Post,$"tasks/{Id(id)}/score/{(completed ? "up" : "down")}",null,ValidId(id), d => d.ValueKind==JsonValueKind.Object ? true : throw new JsonException(),ct);
    public Task<HabiticaApiResult<string>> TestConnectionAsync(HabiticaCredentials c,CancellationToken ct=default) => Send(c,HttpMethod.Get,"user",null,true,d => d.GetProperty("profile").GetProperty("name").GetString() ?? throw new JsonException(),ct);
    private Task<HabiticaApiResult<HabiticaRemoteTask>> TaskRequest(HabiticaCredentials c,HttpMethod method,string path,object? payload,bool valid,CancellationToken ct) => Send(c,method,path,payload,valid,ParseTask,ct);
    private static Dictionary<string,object?> Payload(HabiticaTaskPayload p,bool create)
    {
        var result = new Dictionary<string,object?> { ["text"]=p.Text,["notes"]=p.Notes,["date"]=p.DueAt?.ToUniversalTime().ToString("O") };
        if(create) result["type"]="todo";
        if(p.Alias is not null) result["alias"]=p.Alias;
        if(p.Checklist is not null) result["checklist"]=p.Checklist.Select(x => { var child = new Dictionary<string,object?> { ["text"]=x.Text,["completed"]=x.Completed }; if(!string.IsNullOrWhiteSpace(x.Id)) child["id"]=x.Id; return child; }).ToArray();
        return result;
    }
    public static string? ConfigurationError(string? userId, string? clientId)
    {
        if (!Guid.TryParseExact(userId, "D", out _)) return "Enter a valid Habitica User ID (the UUID from Habitica's API settings).";
        if (clientId == null || !Regex.IsMatch(clientId, @"\A[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}-[A-Za-z0-9][A-Za-z0-9_.-]{0,99}\z"))
            return "The public client identifier must be <author's Habitica User ID>-StopwatchOverlay. A name alone or a UUID alone is incomplete.";
        return null;
    }
    private static bool ValidCredentials(HabiticaCredentials c) => ConfigurationError(c.UserId, c.ClientId) == null && !string.IsNullOrWhiteSpace(c.ApiToken) && !c.ApiToken.Any(char.IsControl);
    private async Task<HabiticaApiResult<T>> Send<T>(HabiticaCredentials c,HttpMethod method,string path,object? payload,bool valid,Func<JsonElement,T> parse,CancellationToken ct)
    {
        if(!ValidCredentials(c)) return new(default,HabiticaErrorKind.Configuration);
        if(!valid) return new(default,HabiticaErrorKind.InvalidInput);
        if(ct.IsCancellationRequested) return new(default,HabiticaErrorKind.Network);
        bool mutation = method != HttpMethod.Get;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            using var request = new HttpRequestMessage(method,new Uri("https://habitica.com/api/v3/"+path));
            request.Headers.Add("x-api-user",c.UserId); request.Headers.Add("x-api-key",c.ApiToken); request.Headers.Add("x-client",c.ClientId);
            if(payload is not null) request.Content=JsonContent.Create(payload);
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
            int status=(int)response.StatusCode;
            var retry=RetryDelay(response);
            if(!response.IsSuccessStatusCode)
            {
                var error=status switch { 401 or 403=>HabiticaErrorKind.Authentication,400 or 422=>HabiticaErrorKind.InvalidInput,404=>HabiticaErrorKind.NotFound,409=>HabiticaErrorKind.Conflict,429=>HabiticaErrorKind.RateLimited,>=500=>HabiticaErrorKind.Server,_=>HabiticaErrorKind.InvalidResponse };
                return new(default,error,status,retry,mutation && (status>=500 || status is >=300 and <400));
            }
            try
            {
                // Bound the response before materializing JSON, including chunked responses.
                using var stream=await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                using var buffer=new MemoryStream(); var bytes=new byte[8192]; int count;
                while((count=await stream.ReadAsync(bytes,timeout.Token).ConfigureAwait(false))>0) { if(buffer.Length+count>2_000_000) throw new JsonException(); buffer.Write(bytes,0,count); }
                using var doc=JsonDocument.Parse(buffer.ToArray());
                if(!doc.RootElement.TryGetProperty("success",out var success)||success.ValueKind!=JsonValueKind.True||!doc.RootElement.TryGetProperty("data",out var data)) throw new JsonException();
                return new(parse(data),HttpStatus:status,RetryAfter:retry);
            }
            catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return new(default,HabiticaErrorKind.InvalidResponse,status,retry,mutation); }
        }
        catch(Exception e) when(e is HttpRequestException or OperationCanceledException or IOException) { return new(default,HabiticaErrorKind.Network,OutcomeUnknown:mutation); }
    }
    private static TimeSpan? RetryDelay(HttpResponseMessage response)
    {
        TimeSpan? retry=response.Headers.RetryAfter?.Delta;
        if(response.Headers.RetryAfter?.Date is { } date) retry=date-DateTimeOffset.UtcNow;
        if(response.Headers.TryGetValues("X-RateLimit-Remaining",out var values)&&values.FirstOrDefault()=="0")
        {
            var delay=TimeSpan.FromSeconds(60);
            if(response.Headers.TryGetValues("X-RateLimit-Reset",out var reset)&&long.TryParse(reset.FirstOrDefault(),out var epoch))
            { try { var remaining=DateTimeOffset.FromUnixTimeSeconds(epoch)-DateTimeOffset.UtcNow; if(remaining>delay) delay=remaining; } catch(ArgumentOutOfRangeException) {} }
            if(retry is null||retry<delay) retry=delay;
        }
        return retry is null ? null : retry<TimeSpan.Zero ? TimeSpan.Zero : retry;
    }
    private static string String(JsonElement d,string key,string fallback="") => d.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String ? v.GetString()! : fallback;
    private static HabiticaRemoteTask ParseTask(JsonElement d)
    {
        var id=String(d,"_id",String(d,"id"));
        if(string.IsNullOrWhiteSpace(id)||!d.TryGetProperty("text",out var text)||text.ValueKind!=JsonValueKind.String) throw new JsonException();
        if(!d.TryGetProperty("completed",out var done)||done.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new JsonException();
        bool completed=done.ValueKind==JsonValueKind.True;
        DateTimeOffset? date=null;
        if(d.TryGetProperty("date",out var dateJson)&&dateJson.ValueKind!=JsonValueKind.Null) { if(!DateTimeOffset.TryParse(dateJson.GetString(),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var parsed)) throw new JsonException(); date=parsed; }
        var checklist=new List<HabiticaChecklistItem>();
        if(!d.TryGetProperty("checklist",out var children)||children.ValueKind!=JsonValueKind.Array) throw new JsonException();
        foreach(var item in children.EnumerateArray())
        { var childId=String(item,"id",String(item,"_id")); if(string.IsNullOrEmpty(childId)||!item.TryGetProperty("completed",out var check)||check.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new JsonException(); checklist.Add(new(childId,String(item,"text"),check.ValueKind==JsonValueKind.True)); }
        return new(id,String(d,"alias"),text.GetString()!,String(d,"notes"),completed,date,checklist);
    }
}

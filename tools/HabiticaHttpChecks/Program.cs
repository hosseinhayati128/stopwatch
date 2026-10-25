using System.Net;
using StopwatchOverlay.Habitica;
await BatchTransportChecks.Run();
var credentials = new HabiticaCredentials("11111111-1111-4111-8111-111111111111", "fake-token", "22222222-2222-4222-8222-222222222222-StopwatchOverlay");
var handler = new FakeHandler();
var client = new HabiticaApiClient(new HttpClient(handler));
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
handler.Json = "{\"success\":true,\"data\":{\"_id\":\"task1\",\"text\":\"Title\",\"completed\":false,\"checklist\":[]}}";
var task = await client.GetTaskAsync(credentials,"task1");
Check(task.Success && task.Data?.Id == "task1", "parse task");
Check(handler.Url == "https://habitica.com/api/v3/tasks/task1" && handler.User == credentials.UserId && handler.Token == credentials.ApiToken && handler.Client == credentials.ClientId,"fixed host and authentication");
var calls = handler.Calls;
Check((await client.GetTaskAsync(credentials with {ClientId="habitica-telegram-bot"},"task1")).Error == HabiticaErrorKind.Configuration && calls == handler.Calls,"reject obsolete client before transmission");
Check((await client.GetTaskAsync(credentials with {UserId="bad"},"task1")).Error == HabiticaErrorKind.Configuration,"reject invalid user");
handler.Status = HttpStatusCode.TooManyRequests; handler.Retry = "120";
var limited = await client.CreateTaskAsync(credentials,new("Title","",null));
Check(limited.Error == HabiticaErrorKind.RateLimited && limited.RetryAfter == TimeSpan.FromSeconds(120) && !limited.OutcomeUnknown,"429 deadline known rejection");
handler.Status = HttpStatusCode.InternalServerError;
Check((await client.DeleteTaskAsync(credentials,"task1")).OutcomeUnknown,"server mutation unknown");
handler.Status = HttpStatusCode.OK; handler.Json="garbage";
Check((await client.SetCompletedAsync(credentials,"task1",true)).OutcomeUnknown,"malformed completion unknown");
Check(!(await client.GetTaskAsync(credentials,"task1")).OutcomeUnknown,"malformed read known");
handler.Throw = true;
Check((await client.CreateTaskAsync(credentials,new("Title","",null))).OutcomeUnknown,"network create unknown");
handler.Throw = false; handler.Json="{\"success\":true,\"data\":{}}";
Check(!(await client.CreateTaskAsync(credentials,new("Title","",null))).Success,"empty task rejected");
handler.Json="{\"success\":true,\"data\":{\"id\":\"task1\",\"text\":\"Title\",\"completed\":true,\"date\":\"2026-10-24T12:00:00Z\",\"checklist\":[{\"id\":\"child1\",\"text\":\"child\",\"completed\":true}]}}";
var child=await client.AddChecklistItemAsync(credentials,"task1","child",true);
Check(child.Success&&child.Data!.Checklist.Single().Completed&&handler.Method=="POST"&&handler.Url.EndsWith("/task1/checklist"),"add checklist explicit state");
await client.UpdateChecklistItemAsync(credentials,"task1","child1","edit",false);
Check(handler.Method=="PUT"&&handler.Url.EndsWith("/task1/checklist/child1"),"update checklist endpoint");
await client.DeleteChecklistItemAsync(credentials,"task1","child1");
Check(handler.Method=="DELETE"&&handler.Url.EndsWith("/task1/checklist/child1"),"delete checklist endpoint");
await client.UpdateTaskAsync(credentials,"task1",new("edit","notes",null));
Check(handler.Method=="PUT"&&handler.Url.EndsWith("/tasks/task1"),"update task endpoint");
handler.Json="{\"success\":true,\"data\":{}}";
await client.SetCompletedAsync(credentials,"task1",false);
Check(handler.Method=="POST"&&handler.Url.EndsWith("/task1/score/down"),"uncomplete endpoint");
Check((await client.DeleteTaskAsync(credentials,"task1")).Success&&handler.Method=="DELETE","delete task endpoint");
handler.Json="{\"success\":true,\"data\":{\"profile\":{\"name\":\"Test display\"}}}";
Check((await client.TestConnectionAsync(credentials)).Data=="Test display","connection returns only display name");
handler.Status=HttpStatusCode.Unauthorized;
Check((await client.GetTaskAsync(credentials,"task1")).Error==HabiticaErrorKind.Authentication,"authentication rejection");
handler.Status=HttpStatusCode.TooManyRequests; handler.Retry=DateTimeOffset.UtcNow.AddMinutes(3).ToString("R");
Check((await client.GetTaskAsync(credentials,"task1")).RetryAfter>TimeSpan.FromSeconds(170),"HTTP date retry deadline");
using(var cancel=new CancellationTokenSource()) {cancel.Cancel(); calls=handler.Calls; Check(!(await client.GetTaskAsync(credentials,"task1",cancel.Token)).OutcomeUnknown&&handler.Calls==calls,"pre-cancelled request not sent");}
if(OperatingSystem.IsWindows()) {
var path=Path.Combine(Path.GetTempPath(),"StopwatchHabiticaChecks-"+Guid.NewGuid().ToString("N"));
try {
 var secrets=new HabiticaSecretStore(path);
 Check(secrets.Write(credentials.UserId,"synthetic-test-token"),"secret write");
 if (secrets.Status.Contains("session-only")) Console.WriteLine("SKIP DPAPI persistence: OS protection unavailable in this execution context; session fallback active."); else { Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Directory.GetFiles(path).Single())).Contains("synthetic-test-token"),"secret file encrypted");
 Check(new HabiticaSecretStore(path).Read(credentials.UserId)=="synthetic-test-token","DPAPI survives new store");
 secrets.Delete(credentials.UserId);
 Check(new HabiticaSecretStore(path).Read(credentials.UserId)==null,"secret deletion persists"); }
 var blocked=Path.Combine(path,"blocked"); File.WriteAllText(blocked,"file");
 var fallback=new HabiticaSecretStore(blocked);
 Check(fallback.Write(credentials.UserId,"synthetic-test-token")&&fallback.Read(credentials.UserId)=="synthetic-test-token"&&fallback.Status.Contains("session-only"),"unavailable store session fallback");
} finally { if(Directory.Exists(path)) Directory.Delete(path,true); }
}
handler.Status=HttpStatusCode.OK;
handler.Json="{\"success\":true,\"data\":{\"id\":\"task1\",\"text\":\"Title\",\"checklist\":[]}}";
Check((await client.GetTaskAsync(credentials,"task1")).Error==HabiticaErrorKind.InvalidResponse,"missing completed rejected before reward reconciliation");
handler.Json="{\"success\":true,\"data\":{\"id\":\"task1\",\"text\":\"Title\",\"completed\":false}}";
Check((await client.GetTaskAsync(credentials,"task1")).Error==HabiticaErrorKind.InvalidResponse,"missing checklist rejected before reconciliation");
await client.CreateTaskAsync(credentials,new("Title","",null,Checklist:new[] {new HabiticaChecklistItem("","child",false)}));
using(var payload=System.Text.Json.JsonDocument.Parse(handler.Body)) Check(!payload.RootElement.GetProperty("checklist")[0].TryGetProperty("id",out _),"new child omits server assigned ID");
Console.WriteLine("HTTP and secret checks passed; no live requests.");
sealed class FakeHandler : HttpMessageHandler {
 public string Body="", Method="", Json="", Url="", User="", Token="", Client="", Retry=""; public int Calls; public bool Throw; public HttpStatusCode Status=HttpStatusCode.OK;
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
 Body=request.Content?.ReadAsStringAsync(ct).GetAwaiter().GetResult()??""; Calls++; Method=request.Method.Method; Url=request.RequestUri!.AbsoluteUri; User=request.Headers.GetValues("x-api-user").Single(); Token=request.Headers.GetValues("x-api-key").Single(); Client=request.Headers.GetValues("x-client").Single();
 if(Throw) throw new HttpRequestException("fake failure");
 var response=new HttpResponseMessage(Status){Content=new StringContent(Json)}; if(Retry!="") response.Headers.TryAddWithoutValidation("Retry-After",Retry); return Task.FromResult(response);
 }
}

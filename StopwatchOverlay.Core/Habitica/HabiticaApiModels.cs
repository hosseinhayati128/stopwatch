namespace StopwatchOverlay.Habitica;
public sealed record HabiticaCredentials(string UserId, string ApiToken, string ClientId);
public enum HabiticaErrorKind { None, Configuration, Authentication, InvalidInput, NotFound, RateLimited, Network, Server, InvalidResponse, Conflict, Storage }
public sealed record HabiticaApiResult<T>(T? Data, HabiticaErrorKind Error = HabiticaErrorKind.None, int? HttpStatus = null, TimeSpan? RetryAfter = null, bool OutcomeUnknown = false) { public bool Success => Error == HabiticaErrorKind.None; }
public sealed record HabiticaChecklistItem(string Id, string Text, bool Completed);
public sealed record HabiticaRemoteTask(string Id, string Alias, string Text, string Notes, bool Completed, DateTimeOffset? DueAt, IReadOnlyList<HabiticaChecklistItem> Checklist);
public sealed record HabiticaTaskPayload(string Text, string Notes, DateTimeOffset? DueAt, string? Alias = null, IReadOnlyList<HabiticaChecklistItem>? Checklist = null);

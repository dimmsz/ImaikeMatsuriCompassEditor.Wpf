using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImaikeMatsuriCompassEditor.Wpf;

public sealed class SupabaseService
{
    private const string BaseUrl = "https://ufypynzhmbrozbxbqxsq.supabase.co";
    private const string PublishableKey = "sb_publishable_lQvSmWOndjOMgL5sd5Xdsw_VZEv2k07";
    private readonly HttpClient _http = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public SupabaseService()
    {
        _http.BaseAddress = new Uri(BaseUrl + "/rest/v1/");
        _http.DefaultRequestHeaders.Add("apikey", PublishableKey);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<Venue>> GetVenuesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("imaike_venues?select=id,venue_no,name,location,latitude,longitude,sort_order&order=sort_order", cancellationToken);
        await EnsureSuccessAsync(response);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var rows = await JsonSerializer.DeserializeAsync<List<VenueRow>>(stream, _json, cancellationToken) ?? [];
        return rows.Select(x => new Venue(x.Id, x.VenueNo, x.Name, x.Location, x.Latitude, x.Longitude)).ToList();
    }

    public async Task<List<EventSchedule>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("imaike_event_schedules?select=id,event_date,start_time,end_time,title,venue_id,description,sort_order,category,verified&order=event_date,start_time,sort_order", cancellationToken);
        await EnsureSuccessAsync(response);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var rows = await JsonSerializer.DeserializeAsync<List<ScheduleRow>>(stream, _json, cancellationToken) ?? [];
        return rows.Select(x => new EventSchedule(x.Id, DateOnly.Parse(x.EventDate), TimeOnly.Parse(x.StartTime), string.IsNullOrWhiteSpace(x.EndTime) ? null : TimeOnly.Parse(x.EndTime), x.Title, x.VenueId, x.Description ?? "", x.Category ?? "", x.Verified)).ToList();
    }

    public async Task UpdateScheduleAsync(EventSchedule schedule, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { category = schedule.Category, verified = schedule.Verified }, _json);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"imaike_event_schedules?id=eq.{schedule.Id}");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        request.Headers.Add("Prefer", "return=minimal");
        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"Supabase API error {(int)response.StatusCode}: {body}");
    }

    private sealed record VenueRow(long Id, [property: JsonPropertyName("venue_no")] short VenueNo, string Name, string? Location, double Latitude, double Longitude);
    private sealed record ScheduleRow(long Id, [property: JsonPropertyName("event_date")] string EventDate, [property: JsonPropertyName("start_time")] string StartTime, [property: JsonPropertyName("end_time")] string? EndTime, string Title, [property: JsonPropertyName("venue_id")] long VenueId, string? Description, int SortOrder, string? Category, bool Verified);
}

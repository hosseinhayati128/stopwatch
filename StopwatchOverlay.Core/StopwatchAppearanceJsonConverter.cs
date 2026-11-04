using System.Text.Json;
using System.Text.Json.Serialization;

namespace StopwatchOverlay;

/// <summary>A malformed appearance field must not invalidate unrelated saved settings.</summary>
public sealed class StopwatchAppearanceJsonConverter : JsonConverter<StopwatchAppearanceSettings>
{
    public override StopwatchAppearanceSettings Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
    {
        using var document=JsonDocument.ParseValue(ref reader);
        var root=document.RootElement;
        var profile=new StopwatchAppearanceSettings();
        if(root.ValueKind!=JsonValueKind.Object)return profile;

        JsonElement Field(string name)
        {
            name=options.PropertyNamingPolicy?.ConvertName(name)??name;
            if(!options.PropertyNameCaseInsensitive)return root.TryGetProperty(name,out var value)?value:default;
            JsonElement result=default;
            foreach(var property in root.EnumerateObject())
                if(string.Equals(name,property.Name,StringComparison.OrdinalIgnoreCase))result=property.Value;
            return result;
        }
        string Text(string name,string fallback)
        {
            var value=Field(name);
            return value.ValueKind==JsonValueKind.String?value.GetString()??fallback:fallback;
        }
        double Number(string name,double fallback)
        {
            var value=Field(name);
            return value.ValueKind==JsonValueKind.Number&&value.TryGetDouble(out double number)&&double.IsFinite(number)?number:fallback;
        }
        profile.Background=Text(nameof(profile.Background),profile.Background);
        profile.Frame=Text(nameof(profile.Frame),profile.Frame);
        profile.Typography=Text(nameof(profile.Typography),profile.Typography);
        profile.Artwork=Text(nameof(profile.Artwork),profile.Artwork);
        profile.HoverControls=Text(nameof(profile.HoverControls),profile.HoverControls);
        profile.BackgroundOpacity=Number(nameof(profile.BackgroundOpacity),profile.BackgroundOpacity);
        profile.UnselectedOpacity=Number(nameof(profile.UnselectedOpacity),profile.UnselectedOpacity);
        var flags=Field(nameof(profile.OpaqueLayers));
        if(flags.ValueKind==JsonValueKind.Number&&flags.TryGetInt32(out int bits))profile.OpaqueLayers=(StopwatchOpaqueLayers)bits;
        profile.Normalize();
        return profile;
    }

    public override void Write(Utf8JsonWriter writer,StopwatchAppearanceSettings value,JsonSerializerOptions options)
    {
        var profile=value.Copy();profile.Normalize();
        string Name(string name)=>options.PropertyNamingPolicy?.ConvertName(name)??name;
        writer.WriteStartObject();
        writer.WriteNumber(Name(nameof(profile.Version)),profile.Version);
        writer.WriteString(Name(nameof(profile.Background)),profile.Background);
        writer.WriteString(Name(nameof(profile.Frame)),profile.Frame);
        writer.WriteString(Name(nameof(profile.Typography)),profile.Typography);
        writer.WriteString(Name(nameof(profile.Artwork)),profile.Artwork);
        writer.WriteString(Name(nameof(profile.HoverControls)),profile.HoverControls);
        writer.WriteNumber(Name(nameof(profile.BackgroundOpacity)),profile.BackgroundOpacity);
        writer.WriteNumber(Name(nameof(profile.UnselectedOpacity)),profile.UnselectedOpacity);
        writer.WriteNumber(Name(nameof(profile.OpaqueLayers)),(int)profile.OpaqueLayers);
        writer.WriteEndObject();
    }
}

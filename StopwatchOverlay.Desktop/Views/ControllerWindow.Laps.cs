using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;

namespace StopwatchOverlay.Desktop.Views;

public sealed record NikaLapRow(string Number, string Split, string Total, bool IsNewest, bool HasNumericTotal)
{
    public bool HasNumericSplit => Split != "—";
}

public partial class ControllerWindow
{
    private ObservableCollection<string>? _lapSource;
    private bool _lapsChanged;
    private IReadOnlyList<NikaLapRow> _lapRows = [];
    private int _firstLapRow = -1;
    private const int LapRowHeight = 42;

    private void LapScroll_Changed(object? sender, ScrollChangedEventArgs e) => ShowVisibleLapRows();

    private void ShowVisibleLapRows()
    {
        // A row can be partly visible at either end of this three-row viewport.
        int first = Math.Clamp((int)Math.Floor(LapScroll.Offset.Y / LapRowHeight), 0, Math.Max(0, _lapRows.Count - 1));
        if (_firstLapRow == first) return;
        _firstLapRow = first;
        Canvas.SetTop(NikaLapTable, first * (double)LapRowHeight);
        int length = Math.Min(4, _lapRows.Count - first);
        var visible = new NikaLapRow[length];
        for (int i = 0; i < length; i++) visible[i] = _lapRows[first + i];
        NikaLapTable.ItemsSource = visible;
    }

    // Two fields without a fraction can be either HH:MM or MM:SS in old workspaces.
    // Without the original display setting, keep that text intact.
    private static readonly Regex RecordedLap = new(
        @"^Lap\s+(?<number>[0-9]+):\s*(?:(?<hours>[0-9]+):)?(?<minutes>[0-9]+):(?<seconds>[0-9]{2})(?:\.(?<fraction>[0-9]{1,7}))?$",
        RegexOptions.CultureInvariant);

    private readonly record struct LapReading(string Number, TimeSpan? Value, int Precision);

    private static LapReading ReadLap(string raw)
    {
        var match = RecordedLap.Match(raw);
        if (!match.Success) return new("—", null, 0);
        string number = match.Groups["number"].Value.PadLeft(2, '0');
        var hours = match.Groups["hours"];
        string fraction = match.Groups["fraction"].Value;
        if ((!hours.Success && fraction.Length == 0) ||
            !long.TryParse(match.Groups["minutes"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long minutes) ||
            (hours.Success && minutes >= 60)) return new(number, null, 0);
        long h = 0;
        int seconds = int.Parse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture);
        if (seconds >= 60 || (hours.Success && !long.TryParse(hours.Value, NumberStyles.None, CultureInfo.InvariantCulture, out h)))
            return new(number, null, 0);
        try
        {
            long ticks = checked(h * TimeSpan.TicksPerHour + minutes * TimeSpan.TicksPerMinute + seconds * TimeSpan.TicksPerSecond +
                int.Parse(fraction.PadRight(7, '0'), CultureInfo.InvariantCulture));
            return new(number, TimeSpan.FromTicks(ticks), fraction.Length);
        }
        catch (OverflowException) { return new(number, null, 0); }
    }

    private static string LapTime(TimeSpan value, int precision)
    {
        string clock = FormattableString.Invariant($"{value.Ticks / TimeSpan.TicksPerHour:00}:{value.Minutes:00}:{value.Seconds:00}");
        if (precision == 0) return clock;
        string fraction = (value.Ticks % TimeSpan.TicksPerSecond).ToString("D7", CultureInfo.InvariantCulture);
        return clock + "." + fraction[..precision];
    }

    internal static IReadOnlyList<NikaLapRow> BuildNikaLaps(IReadOnlyList<string> values)
    {
        var rows = new NikaLapRow[values.Count];
        var previous = new LapReading("", TimeSpan.Zero, 0);
        for (int index = values.Count - 1; index >= 0; index--)
        {
            string raw = values[index] ?? "";
            var current = ReadLap(raw);
            string split = current.Value is { } total && previous.Value is { } last && total >= last
                ? LapTime(total - last, Math.Max(current.Precision, previous.Precision)) : "—";
            rows[index] = new(current.Number, split, current.Value is { } time ? LapTime(time, current.Precision) : raw,
                index == 0, current.Value.HasValue);
            previous = current;
        }
        return rows;
    }

    private void MarkLapsChanged(object? sender, NotifyCollectionChangedEventArgs e) => _lapsChanged = true;

    private void UpdateNavyLaps(ObservableCollection<string> laps)
    {
        if (_lapSource != laps)
        {
            if (_lapSource != null) _lapSource.CollectionChanged -= MarkLapsChanged;
            _lapSource = laps;
            laps.CollectionChanged += MarkLapsChanged;
            _lapsChanged = true;
        }
        if (!_lapsChanged) return;
        _lapsChanged = false;
        int count = laps.Count;
        _lapRows = BuildNikaLaps(laps);
        _firstLapRow = -1;
        LapScroll.IsVisible = LapColumns.IsVisible = NikaLapCount.IsVisible = count > 0;
        LapEmptyState.IsVisible = count == 0;
        NikaLapCount.Text = count == 1 ? "1 lap" : $"{count} laps";
        // The native scrollbar covers the full history while only visible rows create controls.
        LapExtent.Height = count * (double)LapRowHeight;
        LapScroll.Margin = new Thickness(8, 0, count > 3 ? 4 : 20, 9);
        LapScroll.ScrollToHome();
        ShowVisibleLapRows();
    }
}

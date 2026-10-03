using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

public partial class NotesViewerWindow : Window
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _autoCloseTimer;
    private int _countdownSeconds = 30;
    private List<NoteEntry> _allEntries = new();
    private NoteType? _currentFilter = null;

    public NotesViewerWindow() : this(new AppSettings())
    {
    }

    public NotesViewerWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _autoCloseTimer.Tick += AutoCloseTimer_Tick;

        Opened += OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        LoadNotes();
        _autoCloseTimer.Start();
        Focus();
    }

    private void AutoCloseTimer_Tick(object? sender, EventArgs e)
    {
        _countdownSeconds--;
        if (_countdownSeconds <= 0)
        {
            _autoCloseTimer.Stop();
            Close();
            return;
        }

        CountdownText.Text = $"Auto-closing in {_countdownSeconds}s";
    }

    private void LoadNotes()
    {
        string vaultFolder = _settings.ObsidianVaultFolder;
        if (string.IsNullOrWhiteSpace(vaultFolder) || !Directory.Exists(vaultFolder))
        {
            VaultLocationText.Text = "Vault: Not configured";
            EmptyMessageText.Text = "No Obsidian vault selected. Configure your vault in Settings.";
            EmptyMessageText.IsVisible = true;
            return;
        }

        VaultLocationText.Text = $"Vault: {Path.GetFileName(vaultFolder)}/Notes";

        try
        {
            _allEntries = ObsidianNotesSync.LoadAllNotes(vaultFolder, _settings.NotesSubfolder).ToList();
            RenderEntries();
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "NotesViewerWindow.LoadNotes");
            EmptyMessageText.Text = $"Error reading notes: {ex.Message}";
            EmptyMessageText.IsVisible = true;
        }
    }

    private void RenderEntries()
    {
        ItemsContainer.Children.Clear();

        var filtered = _currentFilter.HasValue
            ? _allEntries.Where(e => e.Type == _currentFilter.Value).ToList()
            : _allEntries;

        if (filtered.Count == 0)
        {
            EmptyMessageText.Text = _currentFilter switch
            {
                NoteType.Todo => "No todos found.",
                NoteType.Reminder => "No reminders found.",
                NoteType.Note => "No quick notes found.",
                _ => "No notes, todos, or reminders found in vault."
            };
            EmptyMessageText.IsVisible = true;
            ItemsContainer.Children.Add(EmptyMessageText);
            return;
        }

        EmptyMessageText.IsVisible = false;

        var grouped = filtered
            .GroupBy(e => e.Timestamp.Date)
            .OrderByDescending(g => g.Key);

        DateTime today = DateTime.Today;
        DateTime yesterday = today.AddDays(-1);

        foreach (var group in grouped)
        {
            string dateLabel;
            if (group.Key == today)
                dateLabel = $"Today · {group.Key:yyyy-MM-dd}";
            else if (group.Key == yesterday)
                dateLabel = $"Yesterday · {group.Key:yyyy-MM-dd}";
            else
                dateLabel = group.Key.ToString("yyyy-MM-dd");

            // Date Header
            var dateHeaderBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 8, 0, 6),
                CornerRadius = new CornerRadius(4)
            };
            if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
                dateHeaderBorder.Background = srBrush;
            if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
                dateHeaderBorder.BorderBrush = bBrush;

            var dateHeaderText = new TextBlock
            {
                Text = dateLabel,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold
            };
            if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
                dateHeaderText.Foreground = aBrush;

            dateHeaderBorder.Child = dateHeaderText;
            ItemsContainer.Children.Add(dateHeaderBorder);

            // Entries in this date
            foreach (var entry in group.OrderByDescending(e => e.Timestamp))
            {
                var entryBorder = CreateEntryCard(entry);
                ItemsContainer.Children.Add(entryBorder);
            }
        }
    }

    private Border CreateEntryCard(NoteEntry entry)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 5)
        };
        if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
            border.Background = srBrush;
        if (Application.Current?.TryFindResource("BorderSoftBrush", out var bsb) == true && bsb is IBrush bsBrush)
            border.BorderBrush = bsBrush;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*")
        };

        // Badge / Icon
        string icon;
        string iconBrushKey;
        switch (entry.Type)
        {
            case NoteType.Todo:
                icon = entry.IsCompleted ? "☑" : "☐";
                iconBrushKey = entry.IsCompleted ? "SecondaryTextBrush" : "SuccessBrush";
                break;
            case NoteType.Reminder:
                icon = "⏰";
                iconBrushKey = "DangerTextBrush";
                break;
            case NoteType.Note:
            default:
                icon = "📝";
                iconBrushKey = "AccentBrush";
                break;
        }

        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 8, 0)
        };
        if (Application.Current?.TryFindResource(iconBrushKey, out var ib) == true && ib is IBrush iBrush)
            iconText.Foreground = iBrush;
        Grid.SetColumn(iconText, 0);
        grid.Children.Add(iconText);

        // Timestamp
        var timeText = new TextBlock
        {
            Text = entry.Timestamp.ToString("HH:mm"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 10, 0)
        };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stb) == true && stb is IBrush stBrush)
            timeText.Foreground = stBrush;
        Grid.SetColumn(timeText, 1);
        grid.Children.Add(timeText);

        // Content
        var contentText = new TextBlock
        {
            Text = entry.Text,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        string textBrushKey = entry.IsCompleted ? "SecondaryTextBrush" : "PrimaryTextBrush";
        if (Application.Current?.TryFindResource(textBrushKey, out var tb) == true && tb is IBrush tBrush)
            contentText.Foreground = tBrush;

        if (entry.IsCompleted)
        {
            contentText.TextDecorations = TextDecorations.Strikethrough;
        }

        Grid.SetColumn(contentText, 2);
        grid.Children.Add(contentText);

        border.Child = grid;
        return border;
    }

    private void OnFilterChanged(object? sender, RoutedEventArgs e)
    {
        if (FilterTodosRadio?.IsChecked == true)
            _currentFilter = NoteType.Todo;
        else if (FilterNotesRadio?.IsChecked == true)
            _currentFilter = NoteType.Note;
        else if (FilterRemindersRadio?.IsChecked == true)
            _currentFilter = NoteType.Reminder;
        else
            _currentFilter = null;

        if (IsLoaded)
        {
            RenderEntries();
        }
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Enter or Key.Escape)
        {
            e.Handled = true;
            _autoCloseTimer.Stop();
            Close();
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer.Stop();
        base.OnClosed(e);
    }
}

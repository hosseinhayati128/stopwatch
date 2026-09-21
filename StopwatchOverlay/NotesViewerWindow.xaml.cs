using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using StopwatchOverlay.Themes;

namespace StopwatchOverlay;

public partial class NotesViewerWindow : Window
{
    private readonly AppSettings _settings;
    private readonly bool _pirate;
    private readonly DispatcherTimer _autoCloseTimer;
    private int _countdownSeconds = 30;
    private List<NoteEntry> _allEntries = new();
    private NoteType? _currentFilter = null;

    public NotesViewerWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        OverlayThemeManager.Apply(this, _settings.OverlayTheme, _settings.ThemeMode);
        _pirate = OverlayThemeCatalog.Resolve(_settings.OverlayTheme, _settings.ThemeMode) == OverlayThemeCatalog.Pirate;
        PirateVisual.SetEnabled(this, _pirate);
        if (_pirate)
        {
            Width = 680;
            Height = 650;
        }

        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _autoCloseTimer.Tick += AutoCloseTimer_Tick;
    }

    private void NoteFrame_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // The timer moves below the heading when the frame narrows, while the
        // close action remains reachable and the list retains its own viewport.
        if (HeadingGrid is null || CountdownBadge is null) return;
        bool compact = HeadingGrid.ActualWidth < 560;
        Grid.SetRow(CountdownBadge, compact ? 1 : 0);
        Grid.SetColumn(CountdownBadge, compact ? 0 : 1);
        Grid.SetColumnSpan(ViewerHeading, compact ? 2 : 1);
        CountdownBadge.HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        CountdownBadge.Margin = compact ? new Thickness(0, 6, 8, 0) : new Thickness(0, 0, 8, 0);
        bool shortFrame = _pirate && (NoteFrame.ActualHeight < 400 || NoteFrame.ActualWidth < 400);
        ViewerSkull.Visibility = _pirate && !shortFrame ? Visibility.Visible : Visibility.Collapsed;
        ViewerHeading.TextWrapping = shortFrame ? TextWrapping.NoWrap : TextWrapping.Wrap;
        ViewerHeader.Margin = new Thickness(0, 0, 0, shortFrame ? 6 : 12);
        FilterStrip.Margin = new Thickness(4, 0, 4, shortFrame ? 8 : 12);
        ViewerFooter.Margin = new Thickness(0, shortFrame ? 4 : 10, 0, 0);
        FooterCloseHint.Text = shortFrame ? "Enter / Esc to close" : "Press Enter or Esc to close immediately";
        if (shortFrame)
        {
            NoteFrame.Padding = new Thickness(16);
            PaperSurface.Padding = new Thickness(12);
            ViewerHeading.FontSize = 18;
            FooterCloseHint.FontSize = 11;
            VaultLocationText.FontSize = 11;
        }
        else
        {
            NoteFrame.ClearValue(Border.PaddingProperty);
            PaperSurface.ClearValue(Border.PaddingProperty);
            ViewerHeading.ClearValue(TextBlock.FontSizeProperty);
            FooterCloseHint.ClearValue(TextBlock.FontSizeProperty);
            VaultLocationText.ClearValue(TextBlock.FontSizeProperty);
        }
        foreach (RadioButton filter in new[] { FilterAllRadio, FilterTodosRadio, FilterNotesRadio, FilterRemindersRadio })
        {
            if (shortFrame)
            {
                filter.FontSize = 14;
                filter.Padding = new Thickness(8, 4, 8, 4);
            }
            else
            {
                filter.ClearValue(Control.FontSizeProperty);
                filter.ClearValue(Control.PaddingProperty);
            }
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
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
            EmptyMessageText.Visibility = Visibility.Visible;
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
            EmptyMessageText.Visibility = Visibility.Visible;
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
            EmptyMessageText.Visibility = Visibility.Visible;
            ItemsContainer.Children.Add(EmptyMessageText);
            return;
        }

        EmptyMessageText.Visibility = Visibility.Collapsed;

        // Group entries by Date
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
            dateHeaderBorder.SetResourceReference(Border.BackgroundProperty, "SurfaceRaisedBrush");
            dateHeaderBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

            var dateHeaderText = new TextBlock
            {
                Text = dateLabel,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            dateHeaderText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            dateHeaderText.SetResourceReference(TextBlock.FontFamilyProperty, "AppFontFamily");
            dateHeaderBorder.Child = dateHeaderText;
            if (_pirate)
            {
                dateHeaderBorder.Background = PaperBrush(0xDB, 0xE5, 0xC5, 0x8C);
                dateHeaderBorder.BorderBrush = PaperBrush(0xFF, 0xA1, 0x7B, 0x49);
                dateHeaderBorder.BorderThickness = new Thickness(0, 0, 0, 1);
                dateHeaderBorder.Padding = new Thickness(10, 7, 10, 7);
                dateHeaderBorder.Margin = new Thickness(0, 3, 0, 8);
                dateHeaderText.FontSize = 16;
                dateHeaderText.FontFamily = (FontFamily)FindResource("NotesBodyFont");
                dateHeaderText.Foreground = PaperBrush(0xFF, 0x7E, 0x40, 0x27);
            }
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
        border.SetResourceReference(Border.BackgroundProperty, "SurfaceRaisedBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderSoftBrush");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

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
        iconText.SetResourceReference(TextBlock.ForegroundProperty, iconBrushKey);
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
        timeText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
        timeText.SetResourceReference(TextBlock.FontFamilyProperty, "AppFontFamily");
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
        contentText.SetResourceReference(TextBlock.ForegroundProperty, entry.IsCompleted ? "SecondaryTextBrush" : "PrimaryTextBrush");
        contentText.SetResourceReference(TextBlock.FontFamilyProperty, "AppFontFamily");

        if (entry.IsCompleted)
        {
            contentText.TextDecorations = TextDecorations.Strikethrough;
        }

        Grid.SetColumn(contentText, 2);
        grid.Children.Add(contentText);

        if (_pirate)
        {
            border.Background = PaperBrush(0xB5, 0xE6, 0xC5, 0x8F);
            border.BorderBrush = PaperBrush(0xD9, 0x9B, 0x76, 0x40);
            border.CornerRadius = new CornerRadius(8);
            border.Padding = new Thickness(10, 8, 10, 8);
            border.Margin = new Thickness(0, 0, 0, 7);
            var ink = PaperBrush(0xFF, 0x4A, 0x2C, 0x18);
            var muted = PaperBrush(0xFF, 0x83, 0x6C, 0x48);
            iconText.Text = entry.Type switch
            {
                NoteType.Todo => entry.IsCompleted ? "☑" : "☐",
                NoteType.Reminder => "◷",
                _ => "✎"
            };
            iconText.FontFamily = new FontFamily("Segoe UI Symbol");
            iconText.FontSize = 20;
            iconText.Foreground = entry.IsCompleted ? muted : entry.Type == NoteType.Todo
                ? PaperBrush(0xFF, 0x39, 0x78, 0x69) : PaperBrush(0xFF, 0x8A, 0x4E, 0x2B);
            timeText.FontSize = 13;
            timeText.Margin = new Thickness(0, 4, 10, 0);
            timeText.FontFamily = (FontFamily)FindResource("NotesBodyFont");
            timeText.Foreground = ink;
            contentText.FontSize = 20;
            contentText.FontFamily = (FontFamily)FindResource("NotesBodyFont");
            contentText.Foreground = entry.IsCompleted ? muted : ink;
        }

        border.Child = grid;
        return border;
    }

    private static Brush PaperBrush(byte alpha, byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
        brush.Freeze();
        return brush;
    }

    private void FilterRadio_Checked(object sender, RoutedEventArgs e)
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

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
        {
            e.Handled = true;
            _autoCloseTimer.Stop();
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
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

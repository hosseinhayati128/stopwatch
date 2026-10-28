using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StopwatchOverlay;

public partial class TimerPickerWindow : Window
{
    private readonly IReadOnlyList<TimerSession> _timers;
    private readonly TimerSession? _activeTimer;
    private int _selectedIndex = 0;
    private readonly List<Border> _itemBorders = new();
    private readonly bool _pirate;

    public TimerSession? SelectedTimer { get; private set; }

    public TimerPickerWindow(IReadOnlyList<TimerSession> timers, TimerSession? activeTimer)
    {
        InitializeComponent();
        _pirate = AppThemeManager.IsPirate;
        Themes.PirateVisual.SetEnabled(this, _pirate);
        if (_pirate)
        {
            PickerHint.Foreground = new SolidColorBrush(Color.FromRgb(222, 194, 148));
            PickerHint.FontSize = 13;
            PickerCloseButton.Background = new SolidColorBrush(Color.FromRgb(111, 48, 38));
            PickerCloseButton.Foreground = new SolidColorBrush(Color.FromRgb(246, 230, 191));
        }
        _timers = timers ?? Array.Empty<TimerSession>();
        _activeTimer = activeTimer;

        if (_activeTimer != null)
        {
            int idx = _timers.ToList().IndexOf(_activeTimer);
            if (idx >= 0) _selectedIndex = idx;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PopulateList();
        HighlightSelectedIndex();
    }

    private void PopulateList()
    {
        TimersListPanel.Children.Clear();
        _itemBorders.Clear();

        if (_timers.Count == 0)
            TimersListPanel.Children.Add(new TextBlock {
                Text = "No clocks yet. Create a timer in the controller.", TextWrapping = TextWrapping.Wrap,
                Foreground = PickerHint.Foreground, FontSize = 15, Margin = new Thickness(4, 16, 4, 20)
            });

        for (int i = 0; i < _timers.Count; i++)
        {
            var timer = _timers[i];
            int index = i;
            bool isActive = ReferenceEquals(timer, _activeTimer);

            var border = new Border
            {
                Style = (Style)FindResource("TimerItemBorderStyle"),
                Tag = index
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); // Number badge
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // REC/Dot
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name & category
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Time
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Active badge

            // Number badge
            var badgeBorder = new Border
            {
                Background = (Brush)FindResource("AccentBrush"),
                CornerRadius = new CornerRadius(4),
                Width = 22,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            var badgeText = new TextBlock
            {
                Text = (index + 1) <= 9 ? (index + 1).ToString() : $"{index + 1}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = badgeText;
            Grid.SetColumn(badgeBorder, 0);
            grid.Children.Add(badgeBorder);

            // Status dot
            var statusDot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = timer.IsRunning ? (Brush)FindResource("RecBrush") : Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(statusDot, 1);
            grid.Children.Add(statusDot);

            // Name
            var namePanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 8, 0)
            };
            var nameBlock = new TextBlock
            {
                Text = timer.DisplayName,
                Foreground = (Brush)FindResource("PrimaryTextBrush"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            namePanel.Children.Add(nameBlock);
            if (!string.IsNullOrWhiteSpace(timer.Category) && timer.Category != "General")
            {
                var catBlock = new TextBlock
                {
                    Text = timer.Category,
                    Foreground = (Brush)FindResource("SecondaryTextBrush"),
                    FontSize = 10,
                    Opacity = 0.8
                };
                namePanel.Children.Add(catBlock);
            }
            Grid.SetColumn(namePanel, 2);
            grid.Children.Add(namePanel);

            // Elapsed time
            var timeBlock = new TextBlock
            {
                Text = $"{timer.Elapsed:hh\\:mm\\:ss}",
                FontFamily = (FontFamily)FindResource("ThemeTimerFontFamily"),
                Foreground = timer.IsRunning ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("PrimaryTextBrush"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0)
            };
            Grid.SetColumn(timeBlock, 3);
            grid.Children.Add(timeBlock);

            // Active badge
            if (isActive)
            {
                var activeBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 0, 180, 216)),
                    BorderBrush = (Brush)FindResource("AccentBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var activeText = new TextBlock
                {
                    Text = "ACTIVE",
                    Foreground = (Brush)FindResource("AccentBrush"),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold
                };
                activeBadge.Child = activeText;
                Grid.SetColumn(activeBadge, 4);
                grid.Children.Add(activeBadge);
            }

            border.Child = grid;
            if (_pirate) DecoratePirateCard(border, timer, index, isActive);

            border.MouseEnter += (s, ev) =>
            {
                _selectedIndex = index;
                HighlightSelectedIndex();
            };
            border.MouseLeftButtonDown += (s, ev) =>
            {
                SelectAndClose(index);
            };

            TimersListPanel.Children.Add(border);
            _itemBorders.Add(border);
        }
        TypographyManager.ApplyWindow(this);
    }

    private void DecoratePirateCard(Border border, TimerSession timer, int index, bool active)
    {
        border.Padding = new Thickness(0);
        border.BorderThickness = new Thickness(2);
        border.ToolTip = timer.DisplayName;
        var layers = new Grid();
        layers.Children.Add(new Themes.NoteArtworkBorder { Artwork = "Paper", IsHitTestVisible = false });
        var content = new Grid { Margin = new Thickness(16, 12, 16, 12) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var number = new Border {
            Width = 30, Height = 30, CornerRadius = new CornerRadius(15), VerticalAlignment = VerticalAlignment.Top,
            Background = (Brush)FindResource("PirateBrass"), BorderBrush = new SolidColorBrush(Color.FromRgb(103, 70, 33)), BorderThickness = new Thickness(2),
            Child = new Viewbox { Margin = new Thickness(4), Child = new TextBlock { Text = (index + 1).ToString(), FontSize = 16,
                Foreground = Brushes.Black, FontWeight = FontWeights.Bold } }
        };
        content.Children.Add(number);
        var details = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
        Grid.SetColumn(details, 1);
        content.Children.Add(details);
        details.Children.Add(new TextBlock { Text = timer.DisplayName, FontSize = 21, FontWeight = FontWeights.Bold,
            FontFamily = (FontFamily)FindResource("PirateHeadingFont"), Foreground = new SolidColorBrush(Color.FromRgb(63, 33, 19)),
            TextTrimming = TextTrimming.CharacterEllipsis });
        var status = new WrapPanel { Margin = new Thickness(0, 3, 0, 4) };
        if (active) status.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(32, 115, 98)), CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 7, 2),
            Child = new TextBlock { Text = "ACTIVE", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.White } });
        status.Children.Add(new TextBlock { Text = (timer.IsRunning ? "Running" : "Paused") + (string.IsNullOrWhiteSpace(timer.Category) ? "" : " · " + timer.Category),
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(112, 76, 44)) });
        details.Children.Add(status);
        var time = timer.Mode == 2 ? timer.CountdownRemaining : timer.Elapsed;
        var magnitude = time.Duration();
        details.Children.Add(new TextBlock { Text = $"{(time < TimeSpan.Zero ? "−" : "")}{(long)magnitude.TotalHours:D2}:{magnitude.Minutes:D2}:{magnitude.Seconds:D2}",
            FontFamily = (FontFamily)FindResource("PirateBodyFont"), FontSize = 27, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(50, 29, 15)), TextWrapping = TextWrapping.Wrap });
        layers.Children.Add(content);
        border.Child = layers;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void HighlightSelectedIndex()
    {
        for (int i = 0; i < _itemBorders.Count; i++)
        {
            var b = _itemBorders[i];
            if (_pirate)
            {
                b.Background = Brushes.Transparent;
                b.BorderBrush = i == _selectedIndex ? new SolidColorBrush(Color.FromRgb(45, 160, 140)) : Brushes.Transparent;
                continue;
            }
            if (i == _selectedIndex)
            {
                b.Background = (Brush)FindResource("SurfaceRaisedBrush");
                b.BorderBrush = (Brush)FindResource("AccentBrush");
            }
            else
            {
                b.Background = (Brush)FindResource("SurfaceBrush");
                b.BorderBrush = (Brush)FindResource("BorderSoftBrush");
            }
        }
    }

    private void SelectAndClose(int index)
    {
        if (index >= 0 && index < _timers.Count)
        {
            SelectedTimer = _timers[index];
            DialogResult = true;
            Close();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
            return;
        }

        // Direct digit selection (1-9)
        int digit = -1;
        if (e.Key >= Key.D1 && e.Key <= Key.D9)
            digit = e.Key - Key.D1 + 1;
        else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
            digit = e.Key - Key.NumPad1 + 1;

        if (digit >= 1 && digit <= _timers.Count)
        {
            SelectAndClose(digit - 1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            if (_selectedIndex > 0)
            {
                _selectedIndex--;
                HighlightSelectedIndex();
                _itemBorders[_selectedIndex].BringIntoView();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            if (_selectedIndex < _timers.Count - 1)
            {
                _selectedIndex++;
                HighlightSelectedIndex();
                _itemBorders[_selectedIndex].BringIntoView();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            SelectAndClose(_selectedIndex);
            e.Handled = true;
        }
    }
}

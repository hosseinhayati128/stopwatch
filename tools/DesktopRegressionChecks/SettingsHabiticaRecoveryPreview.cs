using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

internal static partial class SettingsHabiticaNativeChecks
{
    // Sample-only design spike; recovery confirmations never call the integration.
    internal static void RecoveryPreview()
    {
        const string output = "design/one-piece-v2/settings/habitica-recovery-native-preview";
        foreach (int scale in new[] { 70, 90, 125 }) foreach (bool compact in new[] { false, true })
        {
            using var f = new Fixture(scale, compact);
            var font = Field<TextBox>(f.Panel, "_account").FontFamily;
            var scroll = Visual<ScrollViewer>(f.Window, "HabiticaScroll");
            var ink = Brush.Parse("#102B3B"); var muted = Brush.Parse("#655539"); var teal = Brush.Parse("#2D7169"); var amber = Brush.Parse("#865C15");
            TextBlock Label(string text, double size = 13, IBrush? color = null) => new()
            { Text = text, FontFamily = font, FontSize = size, Foreground = color ?? muted, TextWrapping = TextWrapping.Wrap };
            Button Action(string title, bool primary = false)
            {
                var b = new Button { Content = title, FontFamily = font, MinHeight = 36, Padding = new(12, 7), Classes = { "editorAction" } };
                if (primary) b.Classes.Add("editorPrimary"); return b;
            }
            Border Card(string title, params Control[] controls)
            {
                var content = new StackPanel { Spacing = 10, Children = { Label(title, 19, ink) } };
                foreach (var c in controls) content.Children.Add(c);
                return new Border { Padding = new(18), CornerRadius = new(7), Background = Brush.Parse("#40FFF6E4"),
                    BorderBrush = Brush.Parse("#509A7B48"), BorderThickness = new(1), VerticalAlignment = VerticalAlignment.Top, Child = content };
            }
            var overview = Label("Sync enabled on this computer  ·  Your local vault", 13, teal);
            var review = Action("Review existing items", true);
            var batchTitle = Label("12 local items available", 17, ink);
            var batchDetail = Label("Existing todos and reminders can join your Habitica sync. Already linked items will be updated.");
            var progress = new ProgressBar { Minimum = 0, Maximum = 12, Value = 8, Height = 7, IsVisible = false, Foreground = teal };
            var confirm = new StackPanel { Spacing = 10, IsVisible = false };
            var confirmText = Label("Sync these 12 local items? Existing Habitica tasks are not imported or matched by title.", 13, ink);
            var confirmButton = Action("Confirm sync", true); var cancel = Action("Cancel");
            confirm.Children.Add(confirmText); confirm.Children.Add(new WrapPanel { Children = { confirmButton, cancel } });
            confirmButton.Margin = new(0, 0, 8, 0);
            var bulk = Card("Sync existing items", batchTitle, batchDetail, progress, review, confirm,
                Label("New local tasks sync automatically while Habitica sync is enabled.", 12));
            var missingButton = Action("Review missing tasks");
            var missingTitle = Label("No missing linked tasks", 14, teal);
            var missing = Card("Restore missing tasks", missingTitle,
                Label("Recreate tasks that Habitica reported missing, using their local text and checklist."),
                Label("Nothing is recreated automatically.", 12), missingButton);
            var transferButton = Action("Review vault transfer");
            var transfer = Card("Use a copied vault here", Label("Only for the same copied or shared vault", 14, ink),
                Label("Transfer its Habitica sync ownership to this computer."),
                Label("Separate Windows and Ubuntu vaults can sync independently. They do not need a transfer.", 12), transferButton);
            var advanced = new Grid { Children = { missing, transfer } };
            var restoreConfirm = Action("Recreate 2 tasks", true); var restoreCancel = Action("Cancel");
            var transferConfirm = Action("Confirm transfer", true); var transferCancel = Action("Cancel");
            Border Confirmation(string title, string detail, Button accept, Button decline)
            {
                accept.Margin = new(0, 0, 8, 4); decline.Margin = new(0, 0, 0, 4);
                return new Border { IsVisible = false, Padding = new(12), CornerRadius = new(5), BorderThickness = new(1),
                    BorderBrush = Brush.Parse("#809A7B48"), Background = Brush.Parse("#50E8BE70"),
                    Child = new StackPanel { Spacing = 8, Children = { Label(title, 15, ink), Label(detail, 12),
                        new WrapPanel { Children = { accept, decline } } } } };
            }
            var restoreBox = Confirmation("Recreate 2 missing linked tasks?",
                "Their current local text and checklist will be used. Check Habitica first so you do not restore tasks you meant to delete.", restoreConfirm, restoreCancel);
            var transferBox = Confirmation("Use this linked vault here?",
                "Close Stopwatch or disable sync on the old copy first. Finish copying the vault before continuing. This transfers sync ownership; it does not copy files.", transferConfirm, transferCancel);
            ((StackPanel)missing.Child!).Children.Add(restoreBox); ((StackPanel)transfer.Child!).Children.Add(transferBox);
            var tabs = new WrapPanel { Children = { Action("Connection & status"), Action("Sync & recovery", true) } };
            tabs.Children[0].Margin = new(0, 0, 8, 0);
            var contextAction = Action("Connection & status");
            ((StackPanel)bulk.Child!).Children.Insert(4, contextAction);
            var recovery = new StackPanel { Spacing = 14, Children = { overview, bulk, Label("RECOVERY TOOLS", 11), advanced } };
            scroll.Content = null;
            Field<StackPanel>(f.Panel, "_navigatorRoot").Children[0].IsVisible = false;
            Field<StackPanel>(f.Panel, "_navigatorRoot").Children[1].IsVisible = false;
            f.Panel.IsVisible = false;
            var root = new StackPanel { Spacing = 14, Children = {
                new StackPanel { Spacing = 4, Children = { Label("Habitica", 23, ink), Label("Bring existing tasks across and manage linked-task recovery.", 12) } },
                tabs, f.Panel, recovery } };
            scroll.Content = root;
            Visual<TextBlock>(f.Window, "SettingsSaveStatus").Text = "Design preview · sample data only";
            bool? previousStacked = null;
            void Resize()
            {
                bool stacked = scroll.Bounds.Width < 760 * f.Model.Typography.Resolve("Settings").SizePercent / 100;
                if (previousStacked == stacked) return; previousStacked = stacked;
                advanced.ColumnDefinitions = new(stacked ? "*" : "*,14,*"); advanced.RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto");
                Grid.SetColumn(transfer, stacked ? 0 : 2); Grid.SetRow(transfer, stacked ? 2 : 0);
            }
            scroll.SizeChanged += (_, _) => Resize();
            string batchState = "ready";
            void SelectTab(bool showRecovery)
            {
                f.Panel.IsVisible = !showRecovery; recovery.IsVisible = showRecovery;
                tabs.Children[0].Classes.Set("editorPrimary", !showRecovery); tabs.Children[1].Classes.Set("editorPrimary", showRecovery);
                scroll.Offset = default;
            }
            void State(string state)
            {
                batchState = state; confirm.IsVisible = restoreBox.IsVisible = transferBox.IsVisible = false;
                missingButton.IsVisible = transferButton.IsVisible = true;
                missingTitle.Text = state == "blocked" ? "2 linked tasks need attention" : "No missing linked tasks";
                missingTitle.Foreground = state == "blocked" ? amber : teal; missingButton.IsEnabled = state == "blocked";
                overview.Text = state == "paused" ? "Sync paused on this computer · Pending changes are saved" : "Sync enabled on this computer · Your local vault";
                overview.Foreground = state == "paused" ? amber : teal;
                (batchTitle.Text, batchDetail.Text, progress.Value) = state switch
                {
                    "progress" => ("8 of 12 items synced", "4 waiting · Next request in 24 seconds. You can keep using Stopwatch.", 8d),
                    "paused" => ("8 of 12 items synced · Paused", "4 changes are saved locally. Enable sync in Connection & status to continue.", 8d),
                    "blocked" => ("6 of 12 items synced · Needs attention", "4 waiting · 2 linked tasks are missing in Habitica. Review them below before recreating anything.", 6d),
                    "complete" => ("12 of 12 items synced", "All changes in this batch are confirmed by Habitica.", 12d),
                    "empty" => ("No existing items to sync", "Add a todo or reminder to your local vault. Standalone notes stay local.", 0d),
                    "interrupted" => ("Queuing was interrupted", "Already queued changes are saved. Review existing items again to finish adding this batch.", 0d),
                    _ => ("12 local items available", "Todos and reminders in this vault can join your Habitica sync. Already linked items will be updated.", 0d)
                };
                batchTitle.Foreground = state is "blocked" or "interrupted" ? amber : ink;
                progress.IsVisible = state is "progress" or "paused" or "blocked" or "complete";
                progress.Foreground = state is "paused" or "blocked" ? amber : teal;
                review.IsVisible = state is "ready" or "empty" or "interrupted"; review.IsEnabled = state != "empty";
                contextAction.IsVisible = state is "paused" or "blocked";
                contextAction.Content = state == "blocked" ? "Review missing tasks" : "Connection & status";
            }
            void FocusConfirmation(Button b) => Dispatcher.UIThread.Post(() => { b.Focus(); b.BringIntoView(); });
            ((Button)tabs.Children[0]).Click += (_, _) => SelectTab(false);
            ((Button)tabs.Children[1]).Click += (_, _) => SelectTab(true);
            review.Click += (_, _) => { confirm.IsVisible = true; review.IsVisible = false; FocusConfirmation(cancel); };
            cancel.Click += (_, _) => { State(batchState); review.Focus(); };
            confirmButton.Click += (_, _) => State("progress");
            transferButton.Click += (_, _) => {
                restoreBox.IsVisible = false; missingButton.IsVisible = true;
                transferBox.IsVisible = true; transferButton.IsVisible = false; FocusConfirmation(transferCancel);
            };
            missingButton.Click += (_, _) => {
                transferBox.IsVisible = false; transferButton.IsVisible = true;
                restoreBox.IsVisible = true; missingButton.IsVisible = false; FocusConfirmation(restoreCancel);
            };
            restoreCancel.Click += (_, _) => { restoreBox.IsVisible = false; missingButton.IsVisible = true; missingButton.Focus(); };
            transferCancel.Click += (_, _) => { transferBox.IsVisible = false; transferButton.IsVisible = true; transferButton.Focus(); };
            contextAction.Click += (_, _) => { if (batchState == "paused") SelectTab(false); else Click(missingButton); };
            var done = Visual<Button>(f.Window, "SettingsDone");
            void CheckFit()
            {
                Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Recovery preview overflows or loses its viewport.");
                Require(!missing.Bounds.Intersects(transfer.Bounds), "Recovery cards overlap.");
                Require(f.Http.Requests == 0 && f.Secrets.Reads == 0 && f.Secrets.Writes == 0 && f.Saves == 0, "Preview touched integration actions or secrets.");
            }
            void Capture(string name, bool bottom = false)
            {
                Resize(); Pump(100);
                if (bottom) (name == "restore" ? missing : transfer).BringIntoView(); else scroll.Offset = default;
                Pump(100);
                CheckFit();
                if (scale == 90 && (!compact || name is "ready" or "progress" or "restore" or "transfer"))
                    Save(f.Window, (compact ? "compact-" : "") + name, output);
            }
            State("ready"); Capture("ready");
            Require(review.Focus() && !missingButton.IsEnabled, "Default actions or keyboard focus are wrong.");
            var footerPosition = done.TranslatePoint(default, f.Window);
            Click(review); Capture("confirmation");
            Require(confirm.IsVisible && !review.IsVisible && cancel.IsFocused, "Bulk confirmation did not open with safe keyboard focus.");
            Click(cancel); Require(!confirm.IsVisible && review.IsFocused, "Cancel did not restore review.");
            Click(review); Click(confirmButton); Capture("progress");
            State("paused"); Capture("paused"); Click(contextAction); Pump();
            Require(f.Panel.IsEffectivelyVisible && !recovery.IsVisible, "Connection tab is not reachable.");
            Click((Button)tabs.Children[1]); Pump();
            State("blocked"); Capture("blocked"); Click(contextAction); Capture("restore", true);
            Require(restoreBox.IsVisible && !transferBox.IsVisible && restoreCancel.IsFocused, "Missing-task confirmation lost its context or focus.");
            Click(restoreCancel); Require(!restoreBox.IsVisible && missingButton.IsFocused, "Restore cancel did not restore focus.");
            State("ready"); Click(transferButton); Capture("transfer", true);
            Require(transferBox.IsVisible && !restoreBox.IsVisible && transferCancel.IsFocused, "Transfer confirmation lost its context or focus.");
            Click(transferCancel); Require(!transferBox.IsVisible && transferButton.IsFocused, "Transfer cancel did not restore focus.");
            State("complete"); Capture("complete"); State("empty"); Capture("empty");
            Require(!review.IsEnabled, "Empty vault permits bulk sync.");
            State("interrupted"); Capture("interrupted");
            scroll.ScrollToEnd(); Pump(); Require(done.TranslatePoint(default, f.Window) == footerPosition, "Done moved with scrolling.");
            if (compact && scale == 125)
            {
                f.Model.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", f.Model.Typography);
                State("blocked"); Click(missingButton); Resize(); Pump(100); restoreCancel.BringIntoView(); Pump(100); CheckFit();
                foreach (var b in new[] { restoreConfirm, restoreCancel, (Button)tabs.Children[0], (Button)tabs.Children[1] })
                {
                    var text = b.GetVisualDescendants().OfType<TextBlock>().Single();
                    var presenter = b.GetVisualDescendants().OfType<ContentPresenter>().Single();
                    Require(text.TextLayout.WidthIncludingTrailingWhitespace <= presenter.Bounds.Width + 1 && text.TextLayout.Height <= presenter.Bounds.Height + 1,
                        "Recovery action clips enlarged text: " + b.Content);
                }
                Save(f.Window, "large-text", output);
            }
            Console.WriteLine($"PASS recovery design states at {scale}% ({(compact ? "compact" : "regular")})");
        }
    }
}

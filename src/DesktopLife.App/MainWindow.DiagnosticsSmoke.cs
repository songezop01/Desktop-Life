using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopLife.App;

public partial class MainWindow
{
    // Run against the real templates: the read-only state binding is only created
    // when the previously collapsed diagnostics content becomes visible.
    public async Task SmokeDiagnosticsPanel(string root)
    {
        var profile = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var isolatedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DesktopLifeSmoke"));
        if (!string.Equals(Path.GetDirectoryName(profile), isolatedParent, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParseExact(Path.GetFileName(profile), "N", out _))
            throw new InvalidOperationException("Diagnostics panel smoke requires an isolated profile.");
        if (!IsVisible || !IsLoaded)
            throw new InvalidOperationException("Diagnostics panel smoke requires a loaded visible control panel.");

        var sections = DiagnosticsLogicalChildren<Expander>(this).ToArray();
        if (!sections.Any(section => Equals(section.Header, "進階／診斷")))
            throw new InvalidOperationException("Advanced diagnostics section was not found.");
        var expanded = sections.Select(section => section.IsExpanded).ToArray();
        var timerWasRunning = lifeTimer.IsEnabled;
        var originalValues = homeostasisRows.Select(row => row.Value).ToArray();
        var verifiedUpdates = 0;
        lifeTimer.Stop();
        try
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                // Toggle the template's real control, including its binding to
                // IsExpanded, just as keyboard/mouse activation does.
                foreach (var section in sections) SetDiagnosticSectionExpanded(section, true);
                await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.ContextIdle);
                ShowHomeostasis();
                await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.ContextIdle);
                if (!HomeostasisRows.IsVisible)
                    throw new InvalidOperationException("Expanded diagnostics did not expose the state display.");

                var bars = DiagnosticsVisualChildren<ProgressBar>(HomeostasisRows).ToArray();
                if (bars.Length != homeostasisRows.Length)
                    throw new InvalidOperationException($"Expected {homeostasisRows.Length} state displays, found {bars.Length}.");
                for (var index = 0; index < homeostasisRows.Length; index++)
                {
                    var row = homeostasisRows[index];
                    var bar = bars.Single(value => ReferenceEquals(value.DataContext, row));
                    var expression = BindingOperations.GetBindingExpression(bar, RangeBase.ValueProperty);
                    if (expression is null || expression.ParentBinding.Mode != BindingMode.OneWay
                        || expression.Status != BindingStatus.Active || expression.HasError)
                        throw new InvalidOperationException($"Invalid state binding for {row.Name}.");

                    var expected = 11 + index * 7 + cycle;
                    row.Set(expected);
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.DataBind);
                    if (Math.Abs(bar.Value - expected) > 0.000001)
                        throw new InvalidOperationException($"State display did not update for {row.Name}: {bar.Value} instead of {expected}.");
                    verifiedUpdates++;
                }

                foreach (var section in sections.Reverse()) SetDiagnosticSectionExpanded(section, false);
                await Dispatcher.InvokeAsync(UpdateLayout, DispatcherPriority.ContextIdle);
                if (HomeostasisRows.IsVisible)
                    throw new InvalidOperationException("Collapsed diagnostics still exposed the state display.");
            }

            File.WriteAllText(Path.Combine(root, "diagnostics-panel.json"), JsonSerializer.Serialize(new
            {
                Succeeded = true,
                Sections = sections.Select(section => section.Header?.ToString()).ToArray(),
                ExpansionCycles = 3,
                StateDisplays = homeostasisRows.Length,
                VerifiedSourceUpdates = verifiedUpdates,
                BindingMode = "OneWay",
                IsolatedProfile = true,
                CompletedUtc = DateTimeOffset.UtcNow
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            for (var index = 0; index < homeostasisRows.Length; index++) homeostasisRows[index].Set(originalValues[index]);
            for (var index = 0; index < sections.Length; index++) sections[index].IsExpanded = expanded[index];
            if (timerWasRunning) lifeTimer.Start();
        }
    }

    private static void SetDiagnosticSectionExpanded(Expander section, bool expanded)
    {
        section.ApplyTemplate();
        if (section.IsExpanded == expanded) return;
        if (section.Template.FindName("ExpandToggle", section) is not ToggleButton toggle
            || new ToggleButtonAutomationPeer(toggle).GetPattern(PatternInterface.Toggle) is not IToggleProvider provider)
            throw new InvalidOperationException("Diagnostics section has no operable expand control.");
        provider.Toggle();
        if (section.IsExpanded != expanded)
            throw new InvalidOperationException("Diagnostics expand control did not update its section.");
    }

    private static IEnumerable<T> DiagnosticsLogicalChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T found) yield return found;
            foreach (var descendant in DiagnosticsLogicalChildren<T>(child)) yield return descendant;
        }
    }

    private static IEnumerable<T> DiagnosticsVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T found) yield return found;
            foreach (var descendant in DiagnosticsVisualChildren<T>(child)) yield return descendant;
        }
    }
}

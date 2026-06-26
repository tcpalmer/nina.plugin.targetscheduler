using NINA.Plugin.TargetScheduler.Planning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace NINA.Plugin.TargetScheduler.Controls.PlanPreview {

    /// <summary>
    /// Builds the in-UI WPF representation of a <see cref="PlannerReportModel"/>: a tree of collapsible
    /// <see cref="Expander"/>s that mirrors the structure and colors of the HTML planner report.
    /// </summary>
    public static class PlanPreviewReportBuilder {
        // Colors mirror the HTML report CSS.
        private static readonly Brush TextBrush = Frozen("#C7C6C3");
        private static readonly Brush MutedBrush = Frozen("#AAAAAA");
        private static readonly Brush FilteredBrush = Frozen("#E07070");
        private static readonly Brush CandidateBrush = Frozen("#70C070");
        private static readonly Brush CandidateLaterBrush = Frozen("#B8B820");
        private static readonly Brush EvenRowBrush = Frozen("#282828");
        private static readonly Brush BackgroundBrush = Frozen("#1E1E1E");
        private static readonly Brush HeaderLineBrush = Frozen("#555555");
        private static readonly Brush RuleBrush = Frozen("#666666");

        public static FrameworkElement Build(PlannerReportModel model) {
            var root = new StackPanel { Background = BackgroundBrush, Margin = new Thickness(15) };
            TextElement.SetForeground(root, TextBrush);

            if (model == null) {
                return root;
            }

            root.Children.Add(BuildHeaderTable(model));

            foreach (PlannerReportSection section in model.Sections) {
                root.Children.Add(BuildSection(section));
                root.Children.Add(BuildRule());
            }

            return root;
        }

        private static UIElement BuildHeaderTable(PlannerReportModel model) {
            var grid = NewTableGrid(2);
            int row = 0;
            AddKeyValueRow(grid, ref row, "Report Date", model.ReportTime.ToString("MMM d, yyyy HH:mm"), italic: true);
            AddKeyValueRow(grid, ref row, "Plan time", model.InitialAtTime?.ToString("MMM d, yyyy HH:mm") ?? string.Empty, italic: true);
            AddKeyValueRow(grid, ref row, "NINA", model.NinaVersion, italic: true);
            AddKeyValueRow(grid, ref row, "Target Scheduler", model.PluginVersion, italic: true);
            grid.Margin = new Thickness(0, 0, 0, 8);
            return grid;
        }

        private static UIElement BuildSection(PlannerReportSection section) {
            var content = new StackPanel();

            content.Children.Add(Heading("Initial Target Filtering"));
            content.Children.Add(BuildFilteringTable(section));

            if (section.Scoring != null) {
                content.Children.Add(Heading("Target Scoring"));
                content.Children.Add(BuildScoringTable(section.Scoring));
            }

            var expander = new Expander {
                IsExpanded = false,
                Margin = new Thickness(0, 14, 0, 0),
                Header = new TextBlock {
                    Text = $"Plan Report for {section.PlanTime:yyyy-MM-dd} at {section.PlanTime:HH:mm:ss}",
                    FontSize = 14,
                    Foreground = TextBrush
                },
                Content = content
            };

            var sectionPanel = new StackPanel();
            sectionPanel.Children.Add(expander);
            sectionPanel.Children.Add(BuildResult(section));
            return sectionPanel;
        }

        private static UIElement BuildFilteringTable(PlannerReportSection section) {
            var grid = NewTableGrid(3);
            AddHeaderRow(grid, 0, "Project", "Target", "Filtered");

            int row = 1;
            foreach (FilterRow fr in section.FilterRows) {
                MaybeStripe(grid, row, 3);
                AddTextCell(grid, row, 0, fr.Project, TextBrush);
                AddTextCell(grid, row, 1, fr.Target, TextBrush);
                AddTextCell(grid, row, 2, fr.FilteredText, FilterBrush(fr.Kind));
                row++;
            }

            return grid;
        }

        private static UIElement BuildScoringTable(ScoringTable scoring) {
            int columns = 2 + scoring.RuleNames.Count + 1; // Project, Target, rules..., Total
            var grid = NewTableGrid(columns);

            var headers = new string[columns];
            headers[0] = "Project";
            headers[1] = "Target";
            for (int i = 0; i < scoring.RuleNames.Count; i++) {
                headers[2 + i] = scoring.RuleNames[i];
            }
            headers[columns - 1] = "Total";
            AddHeaderRow(grid, 0, headers);

            int row = 1;
            foreach (ScoringRow sr in scoring.Rows) {
                MaybeStripe(grid, row, columns);
                AddTextCell(grid, row, 0, sr.Project, TextBrush);
                AddTextCell(grid, row, 1, sr.Target, TextBrush);
                for (int i = 0; i < scoring.RuleNames.Count; i++) {
                    string cell = sr.RuleScores.TryGetValue(scoring.RuleNames[i], out double ruleScore)
                        ? ruleScore.ToString("F2")
                        : "-";
                    AddTextCell(grid, row, 2 + i, cell, TextBrush, HorizontalAlignment.Right);
                }
                AddTextCell(grid, row, columns - 1, sr.Total.ToString("F2"),
                    sr.IsWinner ? CandidateBrush : FilteredBrush, HorizontalAlignment.Right);
                row++;
            }

            return grid;
        }

        private static UIElement BuildResult(PlannerReportSection section) {
            ResultInfo result = section.Result;
            var panel = new StackPanel();

            switch (result.Kind) {
                case ResultKind.Target:
                    panel.Children.Add(Heading("Selected Target"));
                    var targetGrid = NewTableGrid(2);
                    int trow = 0;
                    AddKeyValueRow(targetGrid, ref trow, "Project", result.Project);
                    AddKeyValueRow(targetGrid, ref trow, "Target", result.Target);
                    if (!string.IsNullOrEmpty(result.ExposureFilterName) || result.ExposureLength > 0) {
                        string expLen = result.ExposureLength > 0 ? $"  {result.ExposureLength}s" : string.Empty;
                        AddKeyValueRow(targetGrid, ref trow, "Exposure", $"{result.ExposureFilterName}{expLen}");
                    }
                    AddKeyValueRow(targetGrid, ref trow, "Start", result.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    AddKeyValueRow(targetGrid, ref trow, "End", result.EndTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    panel.Children.Add(targetGrid);
                    break;

                case ResultKind.Wait:
                    panel.Children.Add(Heading("Wait for Next Target"));
                    string duration = PlannerReport.FormatWaitDuration(result.WaitUntil - section.PlanTime);
                    var waitGrid = NewTableGrid(2);
                    int wrow = 0;
                    AddKeyValueRow(waitGrid, ref wrow, "Next Target", $"{result.NextProject} / {result.NextTarget}");
                    AddKeyValueRow(waitGrid, ref wrow, "Wait Until", $"{result.WaitUntil:yyyy-MM-dd HH:mm:ss}  ({duration})");
                    panel.Children.Add(waitGrid);
                    break;

                default:
                    panel.Children.Add(Heading("Result"));
                    panel.Children.Add(new TextBlock {
                        Text = "No more targets, done for the night.",
                        Foreground = TextBrush,
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                    break;
            }

            return panel;
        }

        // ---------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------

        private static UIElement BuildRule() {
            return new Border {
                Height = 2,
                Background = RuleBrush,
                Margin = new Thickness(0, 18, 0, 0)
            };
        }

        private static TextBlock Heading(string text) {
            return new TextBlock {
                Text = text,
                FontSize = 13,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 14, 0, 0)
            };
        }

        private static Grid NewTableGrid(int columns) {
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
            for (int i = 0; i < columns; i++) {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }
            return grid;
        }

        private static void AddHeaderRow(Grid grid, int row, params string[] headers) {
            EnsureRow(grid, row);
            for (int col = 0; col < headers.Length; col++) {
                var tb = MakeCell(headers[col], MutedBrush,
                    col >= 2 && grid.ColumnDefinitions.Count > 3 ? HorizontalAlignment.Right : HorizontalAlignment.Left, false);
                Grid.SetRow(tb, row);
                Grid.SetColumn(tb, col);
                grid.Children.Add(tb);
            }

            var underline = new Border {
                BorderBrush = HeaderLineBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetRow(underline, row);
            Grid.SetColumn(underline, 0);
            Grid.SetColumnSpan(underline, headers.Length);
            grid.Children.Add(underline);
        }

        private static void AddTextCell(Grid grid, int row, int col, string text, Brush brush, HorizontalAlignment align = HorizontalAlignment.Left) {
            EnsureRow(grid, row);
            var tb = MakeCell(text, brush, align, false);
            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }

        private static void AddKeyValueRow(Grid grid, ref int row, string label, string value, bool italic = false) {
            EnsureRow(grid, row);
            MaybeStripe(grid, row, 2);

            var labelCell = MakeCell(label, MutedBrush, HorizontalAlignment.Left, italic);
            labelCell.Margin = new Thickness(10, 3, 24, 3);
            Grid.SetRow(labelCell, row);
            Grid.SetColumn(labelCell, 0);
            grid.Children.Add(labelCell);

            var valueCell = MakeCell(value, TextBrush, HorizontalAlignment.Left, italic);
            Grid.SetRow(valueCell, row);
            Grid.SetColumn(valueCell, 1);
            grid.Children.Add(valueCell);

            row++;
        }

        private static void MaybeStripe(Grid grid, int row, int colSpan) {
            EnsureRow(grid, row);
            // Mirror the HTML 'tr:nth-of-type(even)' striping: first data row (index 1) is unshaded.
            if ((row - 1) % 2 != 1) {
                return;
            }

            var bg = new Border { Background = EvenRowBrush };
            Grid.SetRow(bg, row);
            Grid.SetColumn(bg, 0);
            Grid.SetColumnSpan(bg, colSpan);
            grid.Children.Add(bg);
        }

        private static TextBlock MakeCell(string text, Brush brush, HorizontalAlignment align, bool italic) {
            return new TextBlock {
                Text = text ?? string.Empty,
                Foreground = brush,
                HorizontalAlignment = align,
                Margin = new Thickness(10, 3, 10, 3),
                FontStyle = italic ? FontStyles.Italic : FontStyles.Normal
            };
        }

        private static void EnsureRow(Grid grid, int row) {
            while (grid.RowDefinitions.Count <= row) {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
        }

        private static Brush FilterBrush(FilterKind kind) {
            switch (kind) {
                case FilterKind.Candidate: return CandidateBrush;
                case FilterKind.CandidateLater: return CandidateLaterBrush;
                default: return FilteredBrush;
            }
        }

        private static Brush Frozen(string hex) {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}

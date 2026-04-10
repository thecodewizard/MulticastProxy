using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace MulticastProxy.DebugViewer;

internal sealed class MainForm : Form
{
    private const string ActivePathMarkerFileName = "debug-events.current.txt";
    private const int MaxVisibleEvents = 2000;

    private readonly BindingList<RelayDebugEvent> _events = [];
    private readonly DebugDataGridView _grid = new();
    private readonly TextBox _pathTextBox = new();
    private readonly TextBox _detailsTextBox = new();
    private readonly Label _statusLabel = new();
    private readonly System.Windows.Forms.Timer _pollTimer = new() { Interval = 750 };
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private string _eventsFilePath;
    private long _lastReadPosition;

    public MainForm()
    {
        _eventsFilePath = GetInitialEventsFilePath();

        Text = "MulticastProxy Debug Viewer";
        Width = 1400;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();

        _pollTimer.Tick += (_, _) => PollEventsFile();
        Shown += (_, _) =>
        {
            ResetViewer(_eventsFilePath);
            _pollTimer.Start();
        };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 5,
            AutoSize = true
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        topPanel.Controls.Add(new Label
        {
            Text = "Events File",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 8, 8, 0)
        }, 0, 0);

        _pathTextBox.Dock = DockStyle.Fill;
        _pathTextBox.Text = _eventsFilePath;
        _pathTextBox.Leave += (_, _) => ResetViewer(_pathTextBox.Text);
        topPanel.Controls.Add(_pathTextBox, 1, 0);

        var browseButton = new Button
        {
            Text = "Browse...",
            AutoSize = true
        };
        browseButton.Click += (_, _) => BrowseForEventsFile();
        topPanel.Controls.Add(browseButton, 2, 0);

        var clearButton = new Button
        {
            Text = "Clear View",
            AutoSize = true
        };
        clearButton.Click += (_, _) => ClearVisibleEvents();
        topPanel.Controls.Add(clearButton, 3, 0);

        _statusLabel.AutoSize = true;
        _statusLabel.Padding = new Padding(12, 8, 0, 0);
        _statusLabel.Text = "Waiting for debug events...";
        topPanel.Controls.Add(_statusLabel, 4, 0);

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 520
        };

        ConfigureGrid();
        splitContainer.Panel1.Controls.Add(_grid);

        _detailsTextBox.Dock = DockStyle.Fill;
        _detailsTextBox.Multiline = true;
        _detailsTextBox.ReadOnly = true;
        _detailsTextBox.ScrollBars = ScrollBars.Both;
        _detailsTextBox.Font = new Font("Consolas", 10);
        splitContainer.Panel2.Controls.Add(_detailsTextBox);

        root.Controls.Add(topPanel, 0, 0);
        root.Controls.Add(splitContainer, 0, 1);

        Controls.Add(root);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.DataSource = _events;
        _grid.SelectionChanged += (_, _) => UpdateSelectedEventDetails();
        _grid.RowPrePaint += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count)
            {
                return;
            }

            if (_grid.Rows[e.RowIndex].DataBoundItem is RelayDebugEvent debugEvent)
            {
                _grid.Rows[e.RowIndex].DefaultCellStyle.BackColor = GetStageColor(debugEvent.Stage);
            }
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RelayDebugEvent.LocalTime),
            HeaderText = "Time",
            Width = 190
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RelayDebugEvent.Stage),
            HeaderText = "Stage",
            Width = 170
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RelayDebugEvent.ShortTraceId),
            HeaderText = "Trace",
            Width = 90
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RelayDebugEvent.Port),
            HeaderText = "Port",
            Width = 70
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RelayDebugEvent.PayloadLength),
            HeaderText = "Bytes",
            Width = 70
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RelayDebugEvent.RemoteEndpoint),
            HeaderText = "Endpoint",
            Width = 220
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RelayDebugEvent.Details),
            HeaderText = "Details",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
    }

    private void BrowseForEventsFile()
    {
        var initialDirectory = Path.GetDirectoryName(_eventsFilePath);

        using var dialog = new OpenFileDialog
        {
            Filter = "Debug event files (*.jsonl)|*.jsonl|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(initialDirectory)
                ? initialDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            FileName = Path.GetFileName(_eventsFilePath)
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ResetViewer(dialog.FileName);
        }
    }

    private void ResetViewer(string path)
    {
        _eventsFilePath = string.IsNullOrWhiteSpace(path) ? GetInitialEventsFilePath() : path.Trim();
        _pathTextBox.Text = _eventsFilePath;
        _lastReadPosition = 0;
        _events.Clear();
        _detailsTextBox.Clear();
        PollEventsFile();
    }

    private void ClearVisibleEvents()
    {
        _events.Clear();
        _detailsTextBox.Clear();
    }

    private void PollEventsFile()
    {
        try
        {
            if (!File.Exists(_eventsFilePath) && TryDiscoverEventsFilePath(out var discoveredPath))
            {
                _eventsFilePath = discoveredPath;
                _pathTextBox.Text = discoveredPath;
            }

            if (!File.Exists(_eventsFilePath))
            {
                _statusLabel.Text = $"Waiting for debug events. Checked {string.Join(" | ", GetCandidateEventsFilePaths())}";
                return;
            }

            using var stream = new FileStream(_eventsFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < _lastReadPosition)
            {
                _lastReadPosition = 0;
                _events.Clear();
            }

            stream.Seek(_lastReadPosition, SeekOrigin.Begin);

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            while (reader.ReadLine() is { Length: > 0 } line)
            {
                var debugEvent = JsonSerializer.Deserialize<RelayDebugEvent>(line, _serializerOptions);
                if (debugEvent is null)
                {
                    continue;
                }

                _events.Add(debugEvent);
                while (_events.Count > MaxVisibleEvents)
                {
                    _events.RemoveAt(0);
                }
            }

            _lastReadPosition = stream.Position;
            _statusLabel.Text = $"Watching {_events.Count} event(s) from {_eventsFilePath}";

            if (_grid.Rows.Count > 0)
            {
                _grid.ClearSelection();
                _grid.Rows[_grid.Rows.Count - 1].Selected = true;
                _grid.FirstDisplayedScrollingRowIndex = _grid.Rows.Count - 1;
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Unable to read {_eventsFilePath}: {ex.Message}";
        }
    }

    private void UpdateSelectedEventDetails()
    {
        if (_grid.CurrentRow?.DataBoundItem is not RelayDebugEvent debugEvent)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Time: {debugEvent.LocalTime}");
        builder.AppendLine($"Stage: {debugEvent.Stage}");
        builder.AppendLine($"Trace: {debugEvent.TraceId}");
        builder.AppendLine($"Port: {debugEvent.Port}");
        builder.AppendLine($"Bytes: {debugEvent.PayloadLength}");
        builder.AppendLine($"Endpoint: {debugEvent.RemoteEndpoint ?? "-"}");

        if (!string.IsNullOrWhiteSpace(debugEvent.Details))
        {
            builder.AppendLine();
            builder.AppendLine("Details");
            builder.AppendLine(debugEvent.Details);
        }

        builder.AppendLine();
        builder.AppendLine("Payload Text");
        builder.AppendLine(string.IsNullOrWhiteSpace(debugEvent.TextPreview) ? "(not printable text)" : debugEvent.TextPreview);

        if (!string.IsNullOrWhiteSpace(debugEvent.RewrittenTextPreview))
        {
            builder.AppendLine();
            builder.AppendLine("Rewritten Text");
            builder.AppendLine(debugEvent.RewrittenTextPreview);
        }

        builder.AppendLine();
        builder.AppendLine("Payload Hex");
        builder.AppendLine(string.IsNullOrWhiteSpace(debugEvent.HexPreview) ? "(empty payload)" : debugEvent.HexPreview);

        _detailsTextBox.Text = builder.ToString();
    }

    private static Color GetStageColor(string stage) => stage switch
    {
        "ServiceStarted" => Color.FromArgb(245, 245, 245),
        "MulticastReceived" => Color.FromArgb(231, 244, 255),
        "MulticastSuppressed" => Color.FromArgb(255, 239, 213),
        "TunnelSent" => Color.FromArgb(233, 248, 236),
        "TunnelReceived" => Color.FromArgb(255, 247, 221),
        "PayloadRewriteApplied" => Color.FromArgb(255, 235, 230),
        "MulticastEmitted" => Color.FromArgb(232, 248, 248),
        "TunnelDropped" or "TunnelReceiveRejected" or "TunnelSendFailed" or "MulticastEmitFailed" => Color.FromArgb(255, 232, 232),
        _ => Color.White
    };

    private static string GetDefaultEventsFilePath()
    {
        var commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(commonApplicationData, "MulticastProxy", "debug-events.jsonl");
    }

    private static string GetInitialEventsFilePath() =>
        TryDiscoverEventsFilePath(out var eventsFilePath)
            ? eventsFilePath
            : GetDefaultEventsFilePath();

    private static bool TryDiscoverEventsFilePath(out string eventsFilePath)
    {
        foreach (var candidate in GetCandidateEventsFilePaths())
        {
            if (File.Exists(candidate))
            {
                eventsFilePath = candidate;
                return true;
            }
        }

        eventsFilePath = GetDefaultEventsFilePath();
        return false;
    }

    private static IEnumerable<string> GetCandidateEventsFilePaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var markerTarget in GetMarkerTargets())
        {
            if (seen.Add(markerTarget))
            {
                yield return markerTarget;
            }
        }

        var defaultPath = GetDefaultEventsFilePath();
        if (seen.Add(defaultPath))
        {
            yield return defaultPath;
        }

        var userTempPath = Path.Combine(Path.GetTempPath(), "MulticastProxy", "debug-events.jsonl");
        if (seen.Add(userTempPath))
        {
            yield return userTempPath;
        }

        if (OperatingSystem.IsWindows())
        {
            var windowsTempPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Temp",
                "MulticastProxy",
                "debug-events.jsonl");

            if (seen.Add(windowsTempPath))
            {
                yield return windowsTempPath;
            }
        }
    }

    private static IEnumerable<string> GetMarkerTargets()
    {
        foreach (var markerPath in GetMarkerPaths())
        {
            if (!File.Exists(markerPath))
            {
                continue;
            }

            var contents = File.ReadAllText(markerPath).Trim();
            if (!string.IsNullOrWhiteSpace(contents))
            {
                yield return contents;
            }
        }
    }

    private static IEnumerable<string> GetMarkerPaths()
    {
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MulticastProxy",
            ActivePathMarkerFileName);

        yield return Path.Combine(Path.GetTempPath(), "MulticastProxy", ActivePathMarkerFileName);

        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Temp",
                "MulticastProxy",
                ActivePathMarkerFileName);
        }
    }
}

internal sealed class DebugDataGridView : DataGridView
{
    public DebugDataGridView()
    {
        DoubleBuffered = true;
    }
}

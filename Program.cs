using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;

namespace PdfViewer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}

internal sealed class MainForm : Form
{
    private static readonly Color AppBg = Color.FromArgb(245, 247, 250);
    private static readonly Color PanelBg = Color.FromArgb(236, 240, 245);
    private static readonly Color BorderColor = Color.FromArgb(213, 220, 230);

    private readonly WebView2 _webView = new();
    private readonly Button _openButton = new();
    private readonly Label _pathLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _creditLabel = new();
    private string? _pendingPath;

    public MainForm(string? startupPath)
    {
        Text = "Tiny PDF Viewer";
        Width = 1000;
        Height = 700;
        MinimumSize = new Size(600, 400);
        BackColor = AppBg;
        StartPosition = FormStartPosition.CenterScreen;

        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;

        _openButton.Text = "Open PDF";
        _openButton.AutoSize = true;
        _openButton.Padding = new Padding(12, 6, 12, 6);
        _openButton.FlatStyle = FlatStyle.Flat;
        _openButton.FlatAppearance.BorderColor = BorderColor;
        _openButton.BackColor = Color.White;
        _openButton.Click += (_, _) => PickAndLoadPdf();

        _pathLabel.AutoSize = false;
        _pathLabel.Dock = DockStyle.Fill;
        _pathLabel.TextAlign = ContentAlignment.MiddleLeft;
        _pathLabel.AutoEllipsis = true;
        _pathLabel.Text = "Drop a PDF here, click Open PDF, or pass a file path as an argument.";

        _statusLabel.AutoSize = true;
        _statusLabel.Text = "Ready";

        _creditLabel.AutoSize = true;
        _creditLabel.Text = "@elias4044";
        _creditLabel.ForeColor = Color.FromArgb(100, 110, 124);

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = PanelBg,
            Padding = new Padding(10, 8, 10, 8),
            ColumnCount = 2,
            RowCount = 1
        };

        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        topBar.Controls.Add(_openButton);
        topBar.Controls.Add(_pathLabel);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            BackColor = PanelBg,
            Padding = new Padding(10, 6, 10, 4),
            ColumnCount = 2,
            RowCount = 1
        };

        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(_statusLabel, 0, 0);
        footer.Controls.Add(_creditLabel, 1, 0);

        var webPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(1)
        };

        _webView.Dock = DockStyle.Fill;
        webPanel.Controls.Add(_webView);

        Controls.Add(webPanel);
        Controls.Add(footer);
        Controls.Add(topBar);

        _pendingPath = startupPath;
        Shown += async (_, _) => await InitializeWebViewAsync();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.O))
        {
            PickAndLoadPdf();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var userDataFolder = Path.Combine(AppContext.BaseDirectory, ".wv2");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);

            if (_webView.CoreWebView2 is not null)
            {
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
            }

            if (!string.IsNullOrWhiteSpace(_pendingPath))
            {
                LoadPdf(_pendingPath!);
            }
            else
            {
                SetStatus("Open or drop a PDF file");
            }
        }
        catch (Exception ex)
        {
            SetStatus("WebView2 initialization failed");
            MessageBox.Show(
                "WebView2 runtime is missing or failed to start.\n\n" + ex.Message,
                "Viewer Initialization Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void PickAndLoadPdf()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            Title = "Select a PDF"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadPdf(dialog.FileName);
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length > 0 && IsPdf(files[0]))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }
        }

        e.Effect = DragDropEffects.None;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true)
        {
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Length > 0)
        {
            LoadPdf(files[0]);
        }
    }

    private void LoadPdf(string path)
    {
        if (!File.Exists(path))
        {
            SetStatus("File not found");
            MessageBox.Show("File not found:\n" + path, "Missing File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!IsPdf(path))
        {
            SetStatus("Invalid file type");
            MessageBox.Show("Please select a .pdf file.", "Invalid File", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _pathLabel.Text = path;

        if (_webView.CoreWebView2 is null)
        {
            _pendingPath = path;
            SetStatus("Viewer is still starting...");
            return;
        }

        try
        {
            var uri = new Uri(path).AbsoluteUri;
            _webView.CoreWebView2.Navigate(uri);
            SetStatus("Loaded: " + Path.GetFileName(path));
        }
        catch
        {
            SetStatus("Opened using system viewer");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private static bool IsPdf(string path) =>
        string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
}

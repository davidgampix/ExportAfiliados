using AfiliadosExportWin.Models;
using AfiliadosExportWin.Services;
using System.Diagnostics;

namespace AfiliadosExportWin;

public partial class MainForm : Form
{
    private readonly IDatabaseService _databaseService;
    private readonly IExcelExportService _excelExportService;
    private CancellationTokenSource? _cancellationTokenSource;

    // Controles
    private ComboBox cmbDatabase = null!;
    private TextBox txtAffiliate = null!;
    private Button btnExport = null!;
    private Button btnCancel = null!;
    private Button btnOpenFile = null!;
    private ProgressBar progressBar = null!;
    private Label lblStatus = null!;
    private ListBox lstLog = null!;
    private string? _lastGeneratedFile;

    public MainForm(IDatabaseService databaseService, IExcelExportService excelExportService)
    {
        _databaseService = databaseService;
        _excelExportService = excelExportService;
        InitializeComponent();
        LoadDatabases();
    }

    private void InitializeComponent()
    {
        this.Text = "Exportador de Afiliados";
        this.Size = new Size(600, 500);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

        // Panel superior
        var panelTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 150,
            Padding = new Padding(10)
        };

        // Label y ComboBox para base de datos
        var lblDatabase = new Label
        {
            Text = "Base de Datos:",
            Location = new Point(10, 20),
            Size = new Size(100, 25)
        };

        cmbDatabase = new ComboBox
        {
            Location = new Point(120, 20),
            Size = new Size(440, 25),
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        // Label y TextBox para afiliado
        var lblAffiliate = new Label
        {
            Text = "Afiliado Raíz:",
            Location = new Point(10, 55),
            Size = new Size(100, 25)
        };

        txtAffiliate = new TextBox
        {
            Location = new Point(120, 55),
            Size = new Size(440, 25),
            PlaceholderText = "Ingrese el nombre del afiliado raíz"
        };

        // Botones
        btnExport = new Button
        {
            Text = "Iniciar Exportación",
            Location = new Point(120, 90),
            Size = new Size(150, 35),
            BackColor = Color.DodgerBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnExport.Click += BtnExport_Click;

        btnCancel = new Button
        {
            Text = "Cancelar",
            Location = new Point(280, 90),
            Size = new Size(100, 35),
            Enabled = false,
            FlatStyle = FlatStyle.Flat
        };
        btnCancel.Click += BtnCancel_Click;

        btnOpenFile = new Button
        {
            Text = "Abrir Archivo",
            Location = new Point(390, 90),
            Size = new Size(100, 35),
            Enabled = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Green,
            ForeColor = Color.White
        };
        btnOpenFile.Click += BtnOpenFile_Click;

        panelTop.Controls.AddRange(new Control[] {
            lblDatabase, cmbDatabase,
            lblAffiliate, txtAffiliate,
            btnExport, btnCancel, btnOpenFile
        });

        // Panel medio - Progreso
        var panelProgress = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(10, 5, 10, 5)
        };

        lblStatus = new Label
        {
            Text = "Listo para exportar",
            Location = new Point(10, 5),
            Size = new Size(560, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };

        progressBar = new ProgressBar
        {
            Location = new Point(10, 30),
            Size = new Size(560, 25),
            Style = ProgressBarStyle.Continuous
        };

        panelProgress.Controls.AddRange(new Control[] { lblStatus, progressBar });

        // Panel inferior - Log
        var panelLog = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        var lblLog = new Label
        {
            Text = "Registro de actividad:",
            Location = new Point(10, 5),
            Size = new Size(150, 20)
        };

        lstLog = new ListBox
        {
            Location = new Point(10, 30),
            Size = new Size(560, 220),
            ScrollAlwaysVisible = true,
            HorizontalScrollbar = true
        };

        panelLog.Controls.AddRange(new Control[] { lblLog, lstLog });

        // Agregar paneles al formulario
        this.Controls.Add(panelLog);
        this.Controls.Add(panelProgress);
        this.Controls.Add(panelTop);

        // Ajustar posición del panel de progreso
        panelProgress.Top = panelTop.Height;
    }

    private void LoadDatabases()
    {
        try
        {
            var databases = _databaseService.GetAvailableDatabases();
            cmbDatabase.Items.Clear();

            foreach (var db in databases)
            {
                cmbDatabase.Items.Add(new ComboBoxItem { Text = db.Name, Value = db.Id });
                if (db.IsDefault)
                {
                    cmbDatabase.SelectedIndex = cmbDatabase.Items.Count - 1;
                }
            }

            if (cmbDatabase.SelectedIndex == -1 && cmbDatabase.Items.Count > 0)
            {
                cmbDatabase.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando bases de datos: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnExport_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtAffiliate.Text))
        {
            MessageBox.Show("Por favor ingrese el nombre del afiliado raíz", "Validación",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (cmbDatabase.SelectedItem == null)
        {
            MessageBox.Show("Por favor seleccione una base de datos", "Validación",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // Preparar UI
            btnExport.Enabled = false;
            btnCancel.Enabled = true;
            btnOpenFile.Enabled = false;
            cmbDatabase.Enabled = false;
            txtAffiliate.Enabled = false;
            progressBar.Value = 0;
            lstLog.Items.Clear();
            _lastGeneratedFile = null;

            var selectedDb = (ComboBoxItem)cmbDatabase.SelectedItem;
            var databaseId = selectedDb.Value;
            var affiliateName = txtAffiliate.Text.Trim();

            AddLog($"Iniciando exportación para: {affiliateName}");
            AddLog($"Base de datos: {selectedDb.Text}");

            _cancellationTokenSource = new CancellationTokenSource();

            // Progress handler
            var progress = new Progress<ExportProgress>(UpdateProgress);

            // Obtener datos
            var data = await _databaseService.GetHierarchicalPlayersAsync(
                affiliateName,
                databaseId,
                progress,
                _cancellationTokenSource.Token);

            if (!data.Any())
            {
                AddLog("No se encontraron datos para el afiliado especificado");
                progressBar.Value = 0;  // Resetear barra de progreso
                lblStatus.Text = "No se encontraron datos";
                MessageBox.Show("No se encontraron datos para el afiliado especificado", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Generar Excel
            var filePath = await _excelExportService.GenerateExcelAsync(
                data,
                affiliateName,
                progress,
                _cancellationTokenSource.Token);

            _lastGeneratedFile = filePath;
            btnOpenFile.Enabled = true;

            AddLog($"Archivo generado: {Path.GetFileName(filePath)}");
            MessageBox.Show($"Exportación completada exitosamente!\n\nArchivo: {Path.GetFileName(filePath)}\nUbicación: {Path.GetDirectoryName(filePath)}",
                "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AddLog("Operación cancelada por el usuario");
            lblStatus.Text = "Operación cancelada";
            progressBar.Value = 0;
        }
        catch (Exception ex)
        {
            AddLog($"Error: {ex.Message}");
            MessageBox.Show($"Error durante la exportación:\n{ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            // Restaurar UI
            btnExport.Enabled = true;
            btnCancel.Enabled = false;
            cmbDatabase.Enabled = true;
            txtAffiliate.Enabled = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        AddLog("Cancelando operación...");
    }

    private void BtnOpenFile_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastGeneratedFile) && File.Exists(_lastGeneratedFile))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _lastGeneratedFile,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir el archivo: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void UpdateProgress(ExportProgress progress)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateProgress(progress));
            return;
        }

        lblStatus.Text = progress.Message;
        progressBar.Value = Math.Min(progress.PercentComplete, 100);

        if (!string.IsNullOrEmpty(progress.Message))
        {
            AddLog(progress.Message);
        }

        if (progress.HasError)
        {
            lblStatus.ForeColor = Color.Red;
        }
        else if (progress.IsComplete)
        {
            lblStatus.ForeColor = Color.Green;
        }
        else
        {
            lblStatus.ForeColor = SystemColors.ControlText;
        }
    }

    private void AddLog(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => AddLog(message));
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        lstLog.Items.Add($"[{timestamp}] {message}");
        lstLog.SelectedIndex = lstLog.Items.Count - 1;
    }

    private class ComboBoxItem
    {
        public string Text { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public override string ToString() => Text;
    }
}
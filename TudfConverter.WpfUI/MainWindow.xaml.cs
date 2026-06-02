using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TudfConverter.WpfUI.ViewModels;

namespace TudfConverter.WpfUI
{
    public partial class MainWindow : Window
    {
        private string _outputFilePath = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls",
                Title = "Select TUDF Input Excel File"
            };

            if (dialog.ShowDialog() == true)
            {
                txtInputFilePath.Text = dialog.FileName;
                btnGenerate.IsEnabled = true;
                panelStatus.Visibility = Visibility.Collapsed;
                borderSuccess.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_outputFilePath) && File.Exists(_outputFilePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_outputFilePath}\"",
                    UseShellExecute = true
                });
            }
        }

        private void BtnViewErrors_Click(object sender, RoutedEventArgs e)
        {
            if (validationTab.IsEnabled)
            {
                mainTabControl.SelectedIndex = 1;
                SetNavActive(isProcessActive: false);
                txtPageTitle.Text = "Validation Results";
            }
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtInputFilePath.Text))
            {
                return;
            }

            btnGenerate.IsEnabled = false;
            btnBrowse.IsEnabled = false;
            
            panelStatus.Visibility = Visibility.Visible;
            borderSuccess.Visibility = Visibility.Collapsed;
            btnViewErrors.Visibility = Visibility.Collapsed;
            pbProgress.Value = 0;
            txtStatus.Text = "Starting process...";

            string inputPath = txtInputFilePath.Text;
            string outputDir = Path.GetDirectoryName(inputPath) ?? "";

            try
            {
                var progress = new Progress<int>(value => pbProgress.Value = value);
                var status = new Progress<string>(message => txtStatus.Text = message);

                var processor = new TudfProcessor();
                var result = await Task.Run(() => processor.ProcessFile(inputPath, outputDir, progress, status));

                if (result.IsSuccess)
                {
                    pbProgress.Value = 100;
                    panelStatus.Visibility = Visibility.Collapsed;
                    borderSuccess.Visibility = Visibility.Visible;
                    
                    _outputFilePath = result.GeneratedFilePath;
                    txtOutputPath.Text = _outputFilePath;
                    txtOutputPath.ToolTip = _outputFilePath;

                    txtTotalRows.Text = result.TotalRows.ToString();
                    txtAcceptedRows.Text = result.AcceptedRows.ToString();
                    txtRejectedRows.Text = result.RejectedRows.ToString();

                    // Populate validation results
                    var vm = new ValidationResultsViewModel();
                    vm.LoadResults(result.ValidationResults ?? new System.Collections.Generic.List<RecordValidationResult>());
                    validationResultsView.DataContext = vm;
                    validationTab.IsEnabled = true;

                    bool hasErrors = result.ValidationResults != null && result.ValidationResults.Any(r => r.Errors.Any());
                    btnViewErrors.Visibility = hasErrors ? Visibility.Visible : Visibility.Collapsed;

                    if (hasErrors)
                    {
                        mainTabControl.SelectedIndex = 1;
                        SetNavActive(isProcessActive: false);
                        txtPageTitle.Text = "Validation Results";
                    }
                }
                else
                {
                    txtStatus.Text = "Error: " + result.ErrorMessage;

                    // Still populate validation results even on failure so user can see details
                    var vm = new ValidationResultsViewModel();
                    vm.LoadResults(result.ValidationResults ?? new System.Collections.Generic.List<RecordValidationResult>());
                    validationResultsView.DataContext = vm;
                    validationTab.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Unexpected Error: " + ex.Message;
            }
            finally
            {
                btnGenerate.IsEnabled = true;
                btnBrowse.IsEnabled = true;
            }
        }

        // ────── Sidebar navigation ──────

        private void BtnNavProcess_Click(object sender, RoutedEventArgs e)
        {
            mainTabControl.SelectedIndex = 0;
            SetNavActive(isProcessActive: true);
            txtPageTitle.Text = "Process File";
        }

        private void BtnNavValidation_Click(object sender, RoutedEventArgs e)
        {
            if (validationTab.IsEnabled)
            {
                mainTabControl.SelectedIndex = 1;
                SetNavActive(isProcessActive: false);
                txtPageTitle.Text = "Validation Results";
            }
        }

        private void SetNavActive(bool isProcessActive)
        {
            // Update Process button appearance
            btnNavProcess.Background = isProcessActive
                ? (System.Windows.Media.Brush)FindResource("SoftAccent")
                : System.Windows.Media.Brushes.Transparent;
            // Update Validation button appearance  
            btnNavValidation.Background = !isProcessActive
                ? (System.Windows.Media.Brush)FindResource("SoftAccent")
                : System.Windows.Media.Brushes.Transparent;
        }

        private void MainTabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
        }
    }
}
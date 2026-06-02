using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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
            SetNavActive(true); // Initialize visual state on startup
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
            if (string.IsNullOrWhiteSpace(txtInputFilePath.Text)) return;

            btnGenerate.IsEnabled = false;
            btnBrowse.IsEnabled = false;

            panelStatus.Visibility = Visibility.Visible;
            borderSuccess.Visibility = Visibility.Collapsed;
            btnViewErrors.Visibility = Visibility.Collapsed;

            pbProgress.IsIndeterminate = true;
            txtStatus.Text = "Initializing processing engine...";

            string inputPath = txtInputFilePath.Text ?? string.Empty;

            // FIX: Explicitly using System.IO.Path to avoid ambiguity with System.Windows.Shapes.Path
            string outputDir = System.IO.Path.GetDirectoryName(inputPath) ?? string.Empty;

            try
            {
                var progress = new Progress<int>(value =>
                {
                    if (pbProgress.IsIndeterminate) pbProgress.IsIndeterminate = false;
                    pbProgress.Value = value;
                });

                var status = new Progress<string>(message => txtStatus.Text = message ?? string.Empty);

                var processor = new TudfProcessor();
                var result = await Task.Run(() => processor.ProcessFile(inputPath, outputDir, progress, status));

                if (result.IsSuccess)
                {
                    pbProgress.IsIndeterminate = false;
                    pbProgress.Value = 100;

                    panelStatus.Visibility = Visibility.Collapsed;
                    borderSuccess.Visibility = Visibility.Visible;

                    _outputFilePath = result.GeneratedFilePath ?? string.Empty;
                    txtOutputPath.Text = _outputFilePath;
                    txtOutputPath.ToolTip = _outputFilePath;

                    txtTotalRows.Text = result.TotalRows.ToString();
                    txtAcceptedRows.Text = result.AcceptedRows.ToString();
                    txtRejectedRows.Text = result.RejectedRows.ToString();

                    var vm = new ValidationResultsViewModel();
                    vm.LoadResults(result.ValidationResults ?? new System.Collections.Generic.List<RecordValidationResult>());
                    validationResultsView.DataContext = vm;
                    validationTab.IsEnabled = true;

                    bool hasErrors = result.ValidationResults != null && result.ValidationResults.Any(r => r.Errors != null && r.Errors.Any());
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
                    pbProgress.IsIndeterminate = false;
                    txtStatus.Text = "Error: " + (result.ErrorMessage ?? "Unknown processing error occurred.");

                    var vm = new ValidationResultsViewModel();
                    vm.LoadResults(result.ValidationResults ?? new System.Collections.Generic.List<RecordValidationResult>());
                    validationResultsView.DataContext = vm;
                    validationTab.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                pbProgress.IsIndeterminate = false;
                txtStatus.Text = "Unexpected Error: " + ex.Message;
            }
            finally
            {
                btnGenerate.IsEnabled = true;
                btnBrowse.IsEnabled = true;
            }
        }

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
            // Toggle Process Button visuals
            btnNavProcess.Background = isProcessActive ? (Brush)FindResource("SoftAccent") : Brushes.Transparent;
            if (btnNavProcess.Template.FindName("ActiveIndicator", btnNavProcess) is Border processIndicator)
                processIndicator.Visibility = isProcessActive ? Visibility.Visible : Visibility.Collapsed;

            // FIX: Explicitly using System.Windows.Shapes.Path to avoid ambiguity
            if (btnNavProcess.Template.FindName("Icon", btnNavProcess) is System.Windows.Shapes.Path processIcon)
                processIcon.Fill = isProcessActive ? (Brush)FindResource("PrimaryAccent") : (Brush)FindResource("TextTertiary");

            if (btnNavProcess.Template.FindName("Label", btnNavProcess) is TextBlock processLabel)
            {
                processLabel.Foreground = isProcessActive ? (Brush)FindResource("PrimaryAccent") : (Brush)FindResource("TextSecondary");
                processLabel.FontWeight = isProcessActive ? FontWeights.SemiBold : FontWeights.Medium;
            }

            // Toggle Validation Button visuals
            btnNavValidation.Background = !isProcessActive ? (Brush)FindResource("SoftAccent") : Brushes.Transparent;
            if (btnNavValidation.Template.FindName("ActiveIndicator", btnNavValidation) is Border valIndicator)
                valIndicator.Visibility = !isProcessActive ? Visibility.Visible : Visibility.Collapsed;

            // FIX: Explicitly using System.Windows.Shapes.Path to avoid ambiguity
            if (btnNavValidation.Template.FindName("Icon", btnNavValidation) is System.Windows.Shapes.Path valIcon)
                valIcon.Fill = !isProcessActive ? (Brush)FindResource("PrimaryAccent") : (Brush)FindResource("TextTertiary");

            if (btnNavValidation.Template.FindName("Label", btnNavValidation) is TextBlock valLabel)
            {
                valLabel.Foreground = !isProcessActive ? (Brush)FindResource("PrimaryAccent") : (Brush)FindResource("TextSecondary");
                valLabel.FontWeight = !isProcessActive ? FontWeights.SemiBold : FontWeights.Medium;
            }
        }
    }
}
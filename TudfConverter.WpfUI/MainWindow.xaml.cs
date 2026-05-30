using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

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
                }
                else
                {
                    txtStatus.Text = "Error: " + result.ErrorMessage;
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
    }
}
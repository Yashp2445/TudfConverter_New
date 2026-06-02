using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;

namespace TudfConverter.WpfUI.ViewModels
{
    public class ValidationResultsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private List<ValidationError> _allErrors = new List<ValidationError>();
        private ObservableCollection<ValidationError> _filteredErrors = new ObservableCollection<ValidationError>();
        private string _searchFilter = "";
        private string _selectedOutcomeFilter = "All Results";

        public ObservableCollection<ValidationError> FilteredErrors
        {
            get => _filteredErrors;
            private set { _filteredErrors = value; OnPropertyChanged(nameof(FilteredErrors)); }
        }

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (_searchFilter == value) return;
                _searchFilter = value;
                OnPropertyChanged(nameof(SearchFilter));
                ApplyFilter();
            }
        }

        public string SelectedOutcomeFilter
        {
            get => _selectedOutcomeFilter;
            set
            {
                if (_selectedOutcomeFilter == value) return;
                _selectedOutcomeFilter = value;
                OnPropertyChanged(nameof(SelectedOutcomeFilter));
                ApplyFilter();
            }
        }

        public int RejectedRowCount { get; private set; }
        public int FieldErrorCount { get; private set; }
        public int SegmentErrorCount { get; private set; }
        public int TotalIssueCount { get; private set; }
        public int FilteredCount => _filteredErrors.Count;

        public ICommand ExportCsvCommand { get; }

        public ValidationResultsViewModel()
        {
            ExportCsvCommand = new RelayCommand(_ => ExportCsv());
        }

        public void LoadResults(List<RecordValidationResult> results)
        {
            if (results == null) results = new List<RecordValidationResult>();
            _allErrors = results.SelectMany(r => r.Errors).ToList();

            RejectedRowCount = results.Count(r => r.IsRecordRejected);
            FieldErrorCount = _allErrors.Count(e => e.Outcome == FailureOutcome.RejectField);
            SegmentErrorCount = _allErrors.Count(e => e.Outcome == FailureOutcome.RejectSegment);
            TotalIssueCount = _allErrors.Count;

            OnPropertyChanged(nameof(RejectedRowCount));
            OnPropertyChanged(nameof(FieldErrorCount));
            OnPropertyChanged(nameof(SegmentErrorCount));
            OnPropertyChanged(nameof(TotalIssueCount));

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filtered = _allErrors.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                var term = _searchFilter.ToLowerInvariant();
                filtered = filtered.Where(e =>
                    (e.ErrorCode ?? "").ToLowerInvariant().Contains(term) ||
                    (e.FieldName ?? "").ToLowerInvariant().Contains(term) ||
                    (e.ErrorMessage ?? "").ToLowerInvariant().Contains(term) ||
                    (e.SegmentTag ?? "").ToLowerInvariant().Contains(term) ||
                    e.RowNumber.ToString().Contains(term));
            }

            switch (_selectedOutcomeFilter)
            {
                case "Rejected Records Only":
                    filtered = filtered.Where(e => e.Outcome == FailureOutcome.RejectRecord);
                    break;
                case "Field Errors Only":
                    filtered = filtered.Where(e => e.Outcome == FailureOutcome.RejectField);
                    break;
                case "Segment Errors Only":
                    filtered = filtered.Where(e => e.Outcome == FailureOutcome.RejectSegment);
                    break;
            }

            FilteredErrors = new ObservableCollection<ValidationError>(filtered);
            OnPropertyChanged(nameof(FilteredCount));
        }

        private void ExportCsv()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV Files|*.csv",
                FileName = "ValidationErrors.csv",
                Title = "Export Validation Errors"
            };
            if (dialog.ShowDialog() != true) return;

            var lines = new List<string> { "Row,Segment,ErrorCode,FieldName,Outcome,Message" };
            foreach (var e in _filteredErrors)
            {
                var msg = (e.ErrorMessage ?? "").Replace("\"", "\"\"");
                lines.Add($"{e.RowNumber},{e.SegmentTag},{e.ErrorCode},{e.FieldName},{e.Outcome},\"{msg}\"");
            }
            File.WriteAllLines(dialog.FileName, lines);
        }

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private class RelayCommand : ICommand
        {
            private readonly Action<object?> _execute;
            public event EventHandler? CanExecuteChanged { add { } remove { } }
            public RelayCommand(Action<object?> execute) => _execute = execute;
            public bool CanExecute(object? parameter) => true;
            public void Execute(object? parameter) => _execute(parameter);
        }
    }
}

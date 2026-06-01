using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TudfConverter.WpfUI
{
    public class TudfProcessor
    {
        public FileProcessingResult ProcessFile(string inputPath, string outputDir, IProgress<int> progress, IProgress<string> status)
        {
            var result = new FileProcessingResult();

            try
            {
                status.Report("Reading Excel file...");
                progress.Report(10);

                var reader = new ExcelReaderService();
                var excelResult = reader.ReadExcelFile(inputPath);

                if (excelResult.Errors.Any())
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = string.Join("\n", excelResult.Errors);
                    return result;
                }

                if (!excelResult.Rows.Any())
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "No data found in Excel.";
                    return result;
                }

                status.Report("Validating data...");
                progress.Report(40);

                var mapper = new ExcelToCustomerRecordMapper();
                var validRecords = new List<CustomerRecord>();

                foreach (var row in excelResult.Rows)
                {
                    var record = mapper.Map(row);

                    bool isValid = ValidateRecord(record);

                    if (isValid)
                        validRecords.Add(record);
                    else
                        result.RejectedRows++;
                }

                if (!validRecords.Any())
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Validation failed. All records were rejected.";
                    return result;
                }

                status.Report("Generating TUDF...");
                progress.Report(70);

                var headerModel = BuildHeaderModel(excelResult.HeaderData, validRecords);

                var generator = new TudfGeneratorService();
                var tudfContent = generator.Generate(validRecords, headerModel);

                status.Report("Saving file...");
                progress.Report(90);

                // Save to "outputs" subfolder next to the input file
                var inputDir = Path.GetDirectoryName(inputPath) ?? "";
                var finalOutputDir = Path.Combine(inputDir, "outputs");
                if (!Directory.Exists(finalOutputDir))
                    Directory.CreateDirectory(finalOutputDir);

                var inputFileName = Path.GetFileNameWithoutExtension(inputPath);
                var outputFileName = $"TUDF_{inputFileName}_{DateTime.Now:yyyyMMdd_HHmmss}.tudf";
                var outputPath = Path.Combine(finalOutputDir, outputFileName);

                File.WriteAllText(outputPath, tudfContent, System.Text.Encoding.ASCII);

                result.IsSuccess = true;
                result.GeneratedFilePath = outputPath;
                result.AcceptedRows = validRecords.Count;
                result.TotalRows = excelResult.Rows.Count;

                status.Report($"Done! {validRecords.Count} records generated. ({result.RejectedRows} rejected)");
                progress.Report(100);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private HeaderSegmentModel BuildHeaderModel(
    Dictionary<string, string> headerData,
    List<CustomerRecord> validRecords)
        {
            // Load base config from appsettings.json
            var settingsHeader = AppSettingsReader.LoadHeaderFromSettings();

            var firstRecord = validRecords.First();

            // Excel header data can still override if present
            headerData.TryGetValue("Member UserId", out var memberUserId);
            headerData.TryGetValue("Name of the CU", out var cuName);
            headerData.TryGetValue("Short Name", out var shortName);
            headerData.TryGetValue("Cycle Identification", out var cycle);
            headerData.TryGetValue("Cycle Date", out var cycleDate);
            headerData.TryGetValue("Date Reported", out var dateReported);

            // Priority: Excel data → appsettings → record fallback
            settingsHeader.MemberUserId = !string.IsNullOrWhiteSpace(memberUserId)
                ? memberUserId
                : !string.IsNullOrWhiteSpace(settingsHeader.MemberUserId)
                    ? settingsHeader.MemberUserId
                    : firstRecord.Account?.CurrentMemberCode ?? "UNKNOWN";

            settingsHeader.ShortName = !string.IsNullOrWhiteSpace(shortName)
                ? shortName
                : !string.IsNullOrWhiteSpace(settingsHeader.ShortName)
                    ? settingsHeader.ShortName
                    : firstRecord.Account?.MemberShortName ?? "";

            settingsHeader.ReportingCycle = !string.IsNullOrWhiteSpace(cycle) ? cycle
                : !string.IsNullOrWhiteSpace(cycleDate) ? cycleDate
                : !string.IsNullOrWhiteSpace(settingsHeader.ReportingCycle)
                    ? settingsHeader.ReportingCycle
                    : "CU";

            if (!string.IsNullOrWhiteSpace(dateReported))
            {
                if (DateTime.TryParseExact(dateReported, "ddMMyyyy", null,
                    System.Globalization.DateTimeStyles.None, out var parsedDate))
                    settingsHeader.DateReportedAndCertified = parsedDate;
                else if (DateTime.TryParse(dateReported, out var parsedDate2))
                    settingsHeader.DateReportedAndCertified = parsedDate2;
            }

            return settingsHeader;
        }

        private bool ValidateRecord(CustomerRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.Name?.FullName)) return false;
            if (string.IsNullOrWhiteSpace(record.Account?.AccountNumber)) return false;
            if (string.IsNullOrWhiteSpace(record.Account?.CurrentMemberCode)) return false;
            if (record.Account.DateOpenedDisbursed == default(DateTime)) return false;
            if (record.Addresses == null || !record.Addresses.Any()) return false;

            var addr = record.Addresses.First();
            if (string.IsNullOrWhiteSpace(addr.AddressLine1)) return false;

            return true;
        }
    }
}
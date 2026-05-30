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

                // FIX: Build the header from Excel header data, not from the first record's account code
                // ExcelReaderService already extracts the header segment values into excelResult.HeaderData
                var headerModel = BuildHeaderModel(excelResult.HeaderData, validRecords);

                var generator = new TudfGeneratorService();
                var tudfContent = generator.Generate(validRecords, headerModel);

                status.Report("Saving file...");
                progress.Report(90);

                var exporter = new FileExportService();
                string outputFileName = $"TUDF_{DateTime.Now:yyyyMMdd_HHmmss}.tudf";
                string outputPath = exporter.ExportTudfAsync(outputDir, tudfContent, outputFileName).GetAwaiter().GetResult();

                result.IsSuccess = true;
                result.GeneratedFilePath = outputPath;
                result.AcceptedRows = validRecords.Count;
                result.TotalRows = excelResult.Rows.Count;

                status.Report($"Successfully generated {validRecords.Count} records. ({result.RejectedRows} rejected)");
                progress.Report(100);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Builds the HeaderSegmentModel using the Excel header data rows (rows above the column header row).
        /// Falls back to first record's account data if specific keys are missing.
        /// </summary>
        private HeaderSegmentModel BuildHeaderModel(
            Dictionary<string, string> headerData,
            List<CustomerRecord> validRecords)
        {
            var firstRecord = validRecords.First();

            // Excel header row provides: "Reporting Member ID", "Short Name", 
            // "Cycle Identification" (or "Cycle Date"), "Date Reported"
            // ExcelReaderService stores them under these keys.
            headerData.TryGetValue("Member UserId", out var memberUserId);
            headerData.TryGetValue("Reporting Member ID", out var altMemberId);
            headerData.TryGetValue("Name of the CU", out var cuName);
            headerData.TryGetValue("Short Name", out var shortName);
            headerData.TryGetValue("Cycle Identification", out var cycle);
            headerData.TryGetValue("Cycle Date", out var cycleDate);
            headerData.TryGetValue("Date Reported", out var dateReported);

            // Resolve Member User ID: prefer explicit header value, else use account member code as-is
            var resolvedMemberUserId = !string.IsNullOrWhiteSpace(memberUserId)
                ? memberUserId
                : (!string.IsNullOrWhiteSpace(altMemberId) ? altMemberId
                   : firstRecord.Account?.CurrentMemberCode ?? "UNKNOWN");

            // Resolve Short Name
            var resolvedShortName = !string.IsNullOrWhiteSpace(shortName)
                ? shortName
                : firstRecord.Account?.MemberShortName ?? string.Empty;

            // Resolve Reporting Cycle (e.g. "CU", "ME", "W1" etc.)
            var resolvedCycle = !string.IsNullOrWhiteSpace(cycle) ? cycle
                : (!string.IsNullOrWhiteSpace(cycleDate) ? cycleDate : "CU");

            // Resolve Date Reported and Certified
            DateTime resolvedDate = firstRecord.Account?.DateReportedAndCertified ?? DateTime.Now;
            if (!string.IsNullOrWhiteSpace(dateReported))
            {
                if (DateTime.TryParseExact(dateReported, "ddMMyyyy", null,
                    System.Globalization.DateTimeStyles.None, out var parsedDate))
                    resolvedDate = parsedDate;
                else if (DateTime.TryParse(dateReported, out var parsedDate2))
                    resolvedDate = parsedDate2;
            }

            return new HeaderSegmentModel
            {
                MemberUserId = resolvedMemberUserId,
                ShortName = resolvedShortName,
                ReportingCycle = resolvedCycle,
                DateReportedAndCertified = resolvedDate,
                MemberData = string.Empty
            };
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
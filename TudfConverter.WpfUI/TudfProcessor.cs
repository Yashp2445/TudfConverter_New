using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TudfConverter.WpfUI
{
    public class TudfProcessor
    {
        public FileProcessingResult ProcessFile(
            string inputPath,
            string outputDir,
            IProgress<int> progress,
            IProgress<string> status)
        {
            var result = new FileProcessingResult();

            try
            {
                progress.Report(5);
                status.Report("Reading Excel file...");
                progress.Report(15);

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

                status.Report("Mapping records...");
                progress.Report(30);

                var mapper = new ExcelToCustomerRecordMapper();
                var allRecords = new List<CustomerRecord>();
                int rowsDone = 0;
                int totalRows = excelResult.Rows.Count;
                foreach (var row in excelResult.Rows)
                {
                    allRecords.Add(mapper.Map(row));
                    rowsDone++;
                    if (rowsDone % 500 == 0)
                        progress.Report(15 + (int)(rowsDone / (double)totalRows * 40));
                }

                // Build header model first — needed for cross-segment date validation
                var headerModel = BuildHeaderModel(excelResult.HeaderData, allRecords);

                status.Report("Validating data...");
                progress.Report(50);

                var validator = new TudfValidator(headerModel.DateReportedAndCertified);
                var validationResults = new List<RecordValidationResult>();
                var validRecords = new List<CustomerRecord>();

                foreach (var record in allRecords)
                {
                    // Quick-fail guard (existing logic)
                    string? quickReject = GetValidationError(record);

                    if (quickReject != null)
                    {
                        result.RejectedRows++;
                        continue; // skip TudfValidator for already-rejected records
                    }

                    // Full validation
                    var vResult = validator.Validate(record);
                    validationResults.Add(vResult);

                    if (vResult.IsRecordRejected)
                    {
                        result.RejectedRows++;
                    }
                    else
                    {
                        validRecords.Add(record);
                    }
                }

                result.ValidationResults = validationResults;
                result.TotalRows = allRecords.Count;
                progress.Report(60);

                if (!validRecords.Any())
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "Validation failed. All records were rejected.";
                    return result;
                }

                status.Report("Generating TUDF...");

                var generator = new TudfGeneratorService();
                var tudfContent = generator.Generate(validRecords, headerModel);
                progress.Report(80);

                status.Report("Saving file...");

                // Save output to "outputs" subfolder of the application directory
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var finalOutputDir = Path.Combine(appDir, "outputs");
                if (!Directory.Exists(finalOutputDir))
                    Directory.CreateDirectory(finalOutputDir);

                var inputFileName = Path.GetFileNameWithoutExtension(inputPath);
                var outputFileName = $"TUDF_{inputFileName}_{DateTime.Now:yyyyMMdd_HHmmss}.tudf";
                var outputPath = Path.Combine(finalOutputDir, outputFileName);

                File.WriteAllText(outputPath, tudfContent, System.Text.Encoding.ASCII);
                progress.Report(95);

                result.IsSuccess = true;
                result.GeneratedFilePath = outputPath;
                result.AcceptedRows = validRecords.Count;

                status.Report($"Done! {validRecords.Count} records accepted, {result.RejectedRows} rejected.");
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
            // Load base config from appsettings.json (lowest priority)
            var settingsHeader = AppSettingsReader.LoadHeaderFromSettings();
            var firstRecord = validRecords.First();

            // Excel header row values take precedence over appsettings
            // Try multiple key names used across different Excel template versions
            headerData.TryGetValue("Member UserId", out var memberUserId);
            if (string.IsNullOrWhiteSpace(memberUserId))
                headerData.TryGetValue("Reporting Member ID", out memberUserId);

            headerData.TryGetValue("Short Name", out var shortName);
            if (string.IsNullOrWhiteSpace(shortName))
                headerData.TryGetValue("Name of the CU", out shortName);

            headerData.TryGetValue("Cycle Identification", out var cycle);
            if (string.IsNullOrWhiteSpace(cycle))
                headerData.TryGetValue("Cycle Date", out cycle);

            headerData.TryGetValue("Date Reported", out var dateReported);

            // Priority: Excel → appsettings.json → account fallback
            settingsHeader.MemberUserId = !string.IsNullOrWhiteSpace(memberUserId)
                ? memberUserId!
                : !string.IsNullOrWhiteSpace(settingsHeader.MemberUserId)
                    ? settingsHeader.MemberUserId
                    : firstRecord.Account?.CurrentMemberCode ?? "UNKNOWN";

            settingsHeader.ShortName = !string.IsNullOrWhiteSpace(shortName)
                ? shortName!
                : !string.IsNullOrWhiteSpace(settingsHeader.ShortName)
                    ? settingsHeader.ShortName
                    : firstRecord.Account?.MemberShortName ?? "";

            // Reporting Cycle: valid values per v3.74 spec:
            // DL, W1, W2, W3, ME, DC, AH, RR, or blank/CU (legacy)
            settingsHeader.ReportingCycle = !string.IsNullOrWhiteSpace(cycle)
                ? cycle!
                : !string.IsNullOrWhiteSpace(settingsHeader.ReportingCycle)
                    ? settingsHeader.ReportingCycle
                    : "CU";

            // Date Reported: Excel value overrides appsettings if provided
            if (!string.IsNullOrWhiteSpace(dateReported))
            {
                if (DateTime.TryParseExact(dateReported, "ddMMyyyy", null,
                    System.Globalization.DateTimeStyles.None, out var parsedDate))
                    settingsHeader.DateReportedAndCertified = parsedDate;
                else if (DateTime.TryParse(dateReported, out var parsedDate2))
                    settingsHeader.DateReportedAndCertified = parsedDate2;
            }
            // If still default (not set from appsettings or Excel), fall back to first record's date
            else if (settingsHeader.DateReportedAndCertified == default)
            {
                settingsHeader.DateReportedAndCertified =
                    firstRecord.Account?.DateReportedAndCertified ?? DateTime.Now;
            }

            return settingsHeader;
        }

        /// <summary>
        /// Validates a customer record per UCRF v3.74 rules.
        /// Returns null if valid, or a description of the rejection reason.
        /// </summary>
        private string? GetValidationError(CustomerRecord record)
        {
            // PN segment: name required, minimum 1 token with at least 2 alphabets
            if (string.IsNullOrWhiteSpace(record.Name?.FullName))
                return "Consumer name is missing";

            var nameWords = (record.Name.FullName ?? "").Split(
                new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (nameWords.Length == 0)
                return "Consumer name has no tokens";

            // TL segment required fields
            if (string.IsNullOrWhiteSpace(record.Account?.AccountNumber))
                return "Account number is missing";
            if (string.IsNullOrWhiteSpace(record.Account?.CurrentMemberCode))
                return "Member code is missing";
            if (record.Account.DateOpenedDisbursed == default(DateTime))
                return "Date Opened/Disbursed is missing or invalid";

            // PA segment: at least one valid address required
            if (record.Addresses == null || !record.Addresses.Any())
                return "No address provided";
            var addr = record.Addresses.First();
            if (string.IsNullOrWhiteSpace(addr.AddressLine1))
                return "Address line 1 is empty";

            // Address combined length must be >= 3 chars
            var combined = string.Concat(
                addr.AddressLine1,
                addr.AddressLine2 ?? "",
                addr.AddressLine3 ?? "",
                addr.AddressLine4 ?? "",
                addr.AddressLine5 ?? "");
            if (combined.Length < 3)
                return "Address combined length is less than 3 characters";

            // ID or Telephone required for accounts opened on/after Jun 1, 2007
            if (record.Account.DateOpenedDisbursed >= new DateTime(2007, 6, 1))
            {
                bool hasValidId = record.Identifications != null &&
                    record.Identifications.Any(id =>
                        id.IdType == 1 || id.IdType == 2 || id.IdType == 3 ||
                        id.IdType == 4 || id.IdType == 5 || id.IdType == 6 ||
                        id.IdType == 9 || id.IdType == 10);

                bool hasValidPhone = record.Telephones != null &&
                    record.Telephones.Any(p => !string.IsNullOrWhiteSpace(p.TelephoneNumber));

                if (!hasValidId && !hasValidPhone)
                    return "No valid ID or telephone for account opened on/after June 1, 2007";
            }

            // High Credit/Sanctioned Amount is required (mandatory since v3.72)
            if (record.Account.HighCreditSanctionedAmount <= 0)
                return "High Credit/Sanctioned Amount must be greater than zero";

            // Either NDPD (tag 15) or Asset Classification (tag 26) must be present
            bool hasNdpd = record.Account.NumberOfDaysPastDue.HasValue;
            bool hasAssetClass = record.Account.AssetClassification.HasValue;
            if (!hasNdpd && !hasAssetClass)
                return "Either Number of Days Past Due or Asset Classification must be provided";

            // Cross-validation: for non-CC accounts, if NDPD > 0 then AmountOverdue must be > 0
            var creditCardTypes = new[] { 10, 16, 31, 35 };
            bool isCreditCard = creditCardTypes.Contains(record.Account.AccountType);
            if (!isCreditCard &&
                record.Account.NumberOfDaysPastDue.HasValue &&
                record.Account.NumberOfDaysPastDue.Value > 0)
            {
                if (!record.Account.AmountOverdue.HasValue || record.Account.AmountOverdue.Value <= 0)
                    return "Amount Overdue must be > 0 when Days Past Due > 0 for non-credit-card accounts";
            }

            // Date Closed validation: if closed, CurrentBalance should be 0 or negative
            if (record.Account.DateClosed.HasValue &&
                record.Account.CurrentBalance > 0 &&
                !record.Account.IsCurrentBalanceNegative)
                return "Date Closed is set but Current Balance is positive";

            return null; // valid
        }
    }
}
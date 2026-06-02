using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace TudfConverter.WpfUI
{
    public class RawExcelRow
    {
        public int RowNumber { get; set; }
        public Dictionary<string, string> Columns { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public class ExcelProcessingResult
    {
        public List<RawExcelRow> Rows { get; } = new List<RawExcelRow>();
        public Dictionary<string, string> HeaderData { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> Errors { get; } = new List<string>();
    }

    public class ExcelToCustomerRecordMapper
    {
        public CustomerRecord Map(RawExcelRow row)
        {
            var record = new CustomerRecord
            {
                RowNumber = row.RowNumber,
                Name = new NameSegmentModel
                {
                    FullName = GetValue(row, ExcelColumnMap.ConsumerName) ?? "",
                    DateOfBirth = TryParseDate(GetValue(row, ExcelColumnMap.DateOfBirth), out var dob) ? dob : (DateTime?)null,
                    Gender = int.TryParse(GetValue(row, ExcelColumnMap.Gender), out int g) ? g : (int?)null
                },
                Identifications = MapIdentifications(row),
                Telephones = MapTelephones(row),
                Emails = MapEmails(row),
                Addresses = MapAddresses(row),
                Account = MapAccount(row),
                AccountHistory = MapAccountHistory(row)
            };
            return record;
        }

        private AccountSegmentModel MapAccount(RawExcelRow row)
        {
            var model = new AccountSegmentModel
            {
                CurrentMemberCode = GetValue(row, ExcelColumnMap.CurrentNewMemberCode) ?? "",
                MemberShortName = GetValue(row, ExcelColumnMap.CurrentNewMemberShortName),
                AccountNumber = GetValue(row, ExcelColumnMap.CurrNewAccountNo) ?? "",
                AccountType = int.TryParse(GetValue(row, ExcelColumnMap.AccountType), out int type) ? type : 0,
                OwnershipIndicator = int.TryParse(GetValue(row, ExcelColumnMap.OwnershipIndicator), out int own) ? own : 1,
                DateOpenedDisbursed = TryParseDate(GetValue(row, ExcelColumnMap.DateOpenedDisbursed), out var d1) ? d1 : default,
                DateOfLastPayment = TryParseDate(GetValue(row, ExcelColumnMap.DateOfLastPayment), out var d2) ? d2 : (DateTime?)null,
                DateClosed = TryParseDate(GetValue(row, ExcelColumnMap.DateClosed), out var d3) ? d3 : (DateTime?)null,
                DateReportedAndCertified = TryParseDate(GetValue(row, ExcelColumnMap.DateReportedAndCertified), out var d4) ? d4 : DateTime.Now,
                HighCreditSanctionedAmount = TryParseLong(GetValue(row, ExcelColumnMap.HighCreditSanctionedAmt), out var hc) ? hc : 0,
                CurrentBalance = TryParseLong(GetValue(row, ExcelColumnMap.CurrentBalance), out var bal) ? bal : 0,
                IsCurrentBalanceNegative = (GetValue(row, ExcelColumnMap.CurrentBalance) ?? "").Trim().StartsWith("-"),
                AmountOverdue = TryParseLong(GetValue(row, ExcelColumnMap.AmtOverdue), out var over) ? over : (long?)null,
                NumberOfDaysPastDue = int.TryParse(GetValue(row, ExcelColumnMap.NoOfDaysPastDue), out int past) ? past : (int?)null,
                OldReportingMemberCode = GetValue(row, ExcelColumnMap.OldMbrCode),
                OldMemberShortName = GetValue(row, ExcelColumnMap.OldMbrShortName),
                OldAccountNumber = GetValue(row, ExcelColumnMap.OldAccNo),
                OldAccountType = int.TryParse(GetValue(row, ExcelColumnMap.OldAccType), out int oldtype) ? oldtype : (int?)null,
                OldOwnershipIndicator = int.TryParse(GetValue(row, ExcelColumnMap.OldOwnershipIndicator), out int oldown) ? oldown : (int?)null,
                SuitFiledWilfulDefault = int.TryParse(GetValue(row, ExcelColumnMap.SuitFiledWilfulDefault), out int suit) ? suit : (int?)null,
                AssetClassification = int.TryParse(GetValue(row, ExcelColumnMap.AssetClassification), out int asset) ? asset : (int?)null,
                ValueOfCollateral = TryParseLong(GetValue(row, ExcelColumnMap.ValueOfCollateral), out var coll) ? coll : (long?)null,
                TypeOfCollateral = int.TryParse(GetValue(row, ExcelColumnMap.TypeOfCollateral), out int coltype) ? coltype : (int?)null,
                CreditLimit = TryParseLong(GetValue(row, ExcelColumnMap.CreditLimit), out var clim) ? clim : (long?)null,
                CashLimit = TryParseLong(GetValue(row, ExcelColumnMap.CashLimit), out var cashlim) ? cashlim : (long?)null,
                RateOfInterest = GetValue(row, ExcelColumnMap.RateOfInterest),
                RepaymentTenure = int.TryParse(GetValue(row, ExcelColumnMap.RepaymentTenure), out int tenure) ? tenure : (int?)null,
                EmiAmount = TryParseLong(GetValue(row, ExcelColumnMap.EmiAmount), out var emi) ? emi : (long?)null,
                WrittenOffAmountTotal = TryParseLong(GetValue(row, ExcelColumnMap.WrittenOffAmountTotal), out var woTot) ? woTot : (long?)null,
                WrittenOffAmountPrincipal = TryParseLong(GetValue(row, ExcelColumnMap.WrittenOffPrincipalAmount), out var woPrin) ? woPrin : (long?)null,
                SettlementAmount = TryParseLong(GetValue(row, ExcelColumnMap.SettlementAmt), out var setl) ? setl : (long?)null,
                PaymentFrequency = int.TryParse(GetValue(row, ExcelColumnMap.PaymentFrequency), out int freq) ? freq : (int?)null,
                ActualPaymentAmount = TryParseLong(GetValue(row, ExcelColumnMap.ActualPaymentAmt), out var act) ? act : (long?)null,
                OccupationCode = int.TryParse(GetValue(row, ExcelColumnMap.OccupationCode), out int occ) ? occ : (int?)null,
                Income = TryParseLong(GetValue(row, ExcelColumnMap.Income), out var inc) ? inc : (long?)null,
                NetGrossIncomeIndicator = GetValue(row, ExcelColumnMap.NetGrossIncomeIndicator),
                MonthlyAnnualIncomeIndicator = GetValue(row, ExcelColumnMap.MonthlyAnnualIncomeIndicator),
                CreditFacilityStatus = int.TryParse(GetValue(row, ExcelColumnMap.CreditFacilityStatus), out int cfs) ? cfs : (int?)null
            };

            return model;
        }

        /// <summary>
        /// Clean address text: remove disallowed characters per UCRF spec Appendix A.
        /// Disallowed in addresses: hyphens (-) and forward slashes (/) that are
        /// common in Indian addresses like "AT/POST" or "TAL-PATAN".
        /// </summary>
        private static string CleanAddressText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            // Only remove hyphens — slashes must be preserved for TUDF format (e.g. AT/POST)
            return text.Replace("-", "");
        }

        /// <summary>
        /// Split address text into lines of max maxLen chars, respecting word boundaries.
        /// </summary>
        private static List<string> SplitAddressIntoLines(string text, int maxLen)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return lines;

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var current = new System.Text.StringBuilder();

            foreach (var word in words)
            {
                if (current.Length == 0)
                {
                    current.Append(word.Length > maxLen ? word.Substring(0, maxLen) : word);
                }
                else if (current.Length + 1 + word.Length <= maxLen)
                {
                    current.Append(' ');
                    current.Append(word);
                }
                else
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    current.Append(word.Length > maxLen ? word.Substring(0, maxLen) : word);
                }
            }

            if (current.Length > 0)
                lines.Add(current.ToString());

            return lines;
        }

        private List<AddressModel> MapAddresses(RawExcelRow row)
        {
            var addresses = new List<AddressModel>();

            var address1Raw = GetValue(row, ExcelColumnMap.AddressLine1);
            if (!string.IsNullOrWhiteSpace(address1Raw))
            {
                var addr = new AddressModel { SegmentIndex = 1 };

                // FIX: Remove hyphens AND forward slashes (both disallowed/problematic in TUDF addresses)
                var cleanAddress = CleanAddressText(address1Raw);
                var lines = SplitAddressIntoLines(cleanAddress, 40);

                addr.AddressLine1 = lines.Count > 0 ? lines[0] : cleanAddress;
                if (lines.Count > 1) addr.AddressLine2 = lines[1];
                if (lines.Count > 2) addr.AddressLine3 = lines[2];
                if (lines.Count > 3) addr.AddressLine4 = lines[3];
                if (lines.Count > 4) addr.AddressLine5 = lines[4];

                addr.StateCode = GetValue(row, ExcelColumnMap.StateCode1);
                addr.PinCode = GetValue(row, ExcelColumnMap.PinCode1);

                if (int.TryParse(GetValue(row, ExcelColumnMap.AddressCategory1), out int category))
                    addr.AddressCategory = category;
                if (int.TryParse(GetValue(row, ExcelColumnMap.ResidenceCode1), out int residence))
                    addr.ResidenceCode = residence;

                addresses.Add(addr);
            }

            // Second address if present
            var address2Raw = GetValue(row, ExcelColumnMap.AddressLine2);
            if (!string.IsNullOrWhiteSpace(address2Raw))
            {
                var addr2 = new AddressModel { SegmentIndex = 2 };
                var cleanAddress2 = CleanAddressText(address2Raw);
                var lines2 = SplitAddressIntoLines(cleanAddress2, 40);

                addr2.AddressLine1 = lines2.Count > 0 ? lines2[0] : cleanAddress2;
                if (lines2.Count > 1) addr2.AddressLine2 = lines2[1];
                if (lines2.Count > 2) addr2.AddressLine3 = lines2[2];
                if (lines2.Count > 3) addr2.AddressLine4 = lines2[3];
                if (lines2.Count > 4) addr2.AddressLine5 = lines2[4];

                addr2.StateCode = GetValue(row, ExcelColumnMap.StateCode2);
                addr2.PinCode = GetValue(row, ExcelColumnMap.PinCode2);

                if (int.TryParse(GetValue(row, ExcelColumnMap.AddressCategory2), out int category2))
                    addr2.AddressCategory = category2;
                if (int.TryParse(GetValue(row, ExcelColumnMap.ResidenceCode2), out int residence2))
                    addr2.ResidenceCode = residence2;

                addresses.Add(addr2);
            }

            return addresses;
        }

        private List<IdentificationModel> MapIdentifications(RawExcelRow row)
        {
            var ids = new List<IdentificationModel>();
            int index = 1;

            void AddId(string col, int typeCode, string? issueCol = null, string? expCol = null)
            {
                if (ids.Count >= 8) return;
                var val = GetValue(row, col);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    var idModel = new IdentificationModel { SegmentIndex = index++, IdType = typeCode, IdNumber = val };
                    if (issueCol != null && TryParseDate(GetValue(row, issueCol), out var issueDate)) idModel.IssueDate = issueDate;
                    if (expCol != null && TryParseDate(GetValue(row, expCol), out var expDate)) idModel.ExpirationDate = expDate;
                    ids.Add(idModel);
                }
            }

            // ID types per UCRF v3.74:
            // 01=PAN, 02=Passport, 03=VoterID, 04=DrivingLicense, 05=RationCard,
            // 06=UID/Aadhaar, 09=CKYC, 10=NREGA(G RAM G)
            // Note: types 07 and 08 (Additional ID) cause "Reject Segment" per spec — do NOT submit them
            AddId(ExcelColumnMap.IncomeTaxIdNumber, 1);
            AddId(ExcelColumnMap.PassportNumber, 2, ExcelColumnMap.PassportIssueDate, ExcelColumnMap.PassportExpiryDate);
            AddId(ExcelColumnMap.VoterIdNumber, 3);
            AddId(ExcelColumnMap.DrivingLicenseNumber, 4, ExcelColumnMap.DrivingLicenseIssueDate, ExcelColumnMap.DrivingLicenseExpiryDate);
            AddId(ExcelColumnMap.RationCardNumber, 5);
            AddId(ExcelColumnMap.UniversalIdNumber, 6);

            // FIX: CKYC is type 09, NREGA is type 10 (per v3.71 correction)
            // Additional ID #1 and #2 (types 07/08) are marked "For Future Use" = rejected by CIC
            // So we only submit CKYC and NREGA if present
            AddId(ExcelColumnMap.Ckyc, 9);
            AddId(ExcelColumnMap.NregaCardNumber, 10);

            return ids;
        }

        private List<TelephoneModel> MapTelephones(RawExcelRow row)
        {
            var phones = new List<TelephoneModel>();
            int index = 1;

            void AddPhone(string col, string typeCode, string? extCol = null)
            {
                if (phones.Count >= 10) return;
                var val = GetValue(row, col);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    var phoneModel = new TelephoneModel { SegmentIndex = index++, TelephoneType = typeCode, TelephoneNumber = val };
                    if (extCol != null) phoneModel.TelephoneExtension = GetValue(row, extCol);
                    phones.Add(phoneModel);
                }
            }

            // Telephone types: 01=Mobile, 02=Home, 03=Office (04=Other not in spec v3.74)
            AddPhone(ExcelColumnMap.TelephoneNoMobile, "01");
            AddPhone(ExcelColumnMap.TelephoneNoResidence, "02");
            AddPhone(ExcelColumnMap.TelephoneNoOffice, "03", ExcelColumnMap.ExtensionOffice);
            // Note: type "04" is not in the v3.74 spec catalogue — map to "00" (Not Classified)
            AddPhone(ExcelColumnMap.TelephoneNoOther, "00", ExcelColumnMap.ExtensionOther);

            return phones;
        }

        private List<EmailModel> MapEmails(RawExcelRow row)
        {
            var emails = new List<EmailModel>();
            var email1 = GetValue(row, ExcelColumnMap.EmailId1);
            if (!string.IsNullOrWhiteSpace(email1)) emails.Add(new EmailModel { SegmentIndex = 1, EmailId = email1 });
            var email2 = GetValue(row, ExcelColumnMap.EmailId2);
            if (!string.IsNullOrWhiteSpace(email2)) emails.Add(new EmailModel { SegmentIndex = 2, EmailId = email2 });
            return emails;
        }

        private List<AccountHistoryModel> MapAccountHistory(RawExcelRow row)
        {
            var history = new List<AccountHistoryModel>();
            int index = 1;

            // Spec v3.74: TH segment can occur maximum 47 times per record (H01 to H47)
            for (int i = 1; i <= 47; i++)
            {
                var dpdKey = $"Month{i}_DPD";
                var balKey = $"Month{i}_Balance";

                if (row.Columns.ContainsKey(dpdKey) || row.Columns.ContainsKey(balKey))
                {
                    TryParseLong(GetValue(row, balKey), out var bal);
                    history.Add(new AccountHistoryModel
                    {
                        SegmentIndex = index++,
                        AccountHistoryDate = default,
                        AssetClassificationNdpd = GetValue(row, dpdKey) ?? string.Empty,
                        CurrentBalance = bal,
                        IsCurrentBalanceNegative = false
                    });
                }
            }

            return history;
        }

        private string? GetValue(RawExcelRow row, string column)
        {
            if (row.Columns.TryGetValue(column, out var val)) return val?.Trim();
            return null;
        }

        /// <summary>
        /// Parse date from various formats. Primary format is ddMMyyyy (as stored in Excel text cells).
        /// </summary>
        private static bool TryParseDate(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            // Primary: ddMMyyyy (UCRF standard format, also how Excel text cells store it)
            if (DateTime.TryParseExact(value, "ddMMyyyy", null, System.Globalization.DateTimeStyles.None, out date)) return true;
            // Fallback formats
            if (DateTime.TryParseExact(value, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out date)) return true;
            if (DateTime.TryParseExact(value, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out date)) return true;
            if (DateTime.TryParseExact(value, "MM/dd/yyyy", null, System.Globalization.DateTimeStyles.None, out date)) return true;
            if (DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            {
                date = dt;
                return true;
            }
            return false;
        }

        private static bool TryParseLong(string? value, out long result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var cleanValue = new string(value.Where(c => char.IsDigit(c) || c == '-').ToArray());
            return long.TryParse(cleanValue, out result);
        }
    }

    public class ExcelReaderService
    {
        public ExcelProcessingResult ReadExcelFile(string filePath)
        {
            var result = new ExcelProcessingResult();

            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                {
                    result.Errors.Add("Excel file is empty or missing worksheets.");
                    return result;
                }

                IXLRow? headerRow = null;
                var knownHeaderKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Name of the CU", "Member UserId", "Cycle Date", "Cycle Identifier",
                    "Reporting Member ID", "Short Name", "Cycle Identification", "Date Reported",
                    "Reporting Password", "Authentication Method", "Future Use", "Member Data"
                };
                int skipNextRowsCount = 0;

                for (int r = 1; r <= 50; r++)
                {
                    if (skipNextRowsCount > 0)
                    {
                        skipNextRowsCount--;
                        continue;
                    }

                    var row = worksheet.Row(r);
                    bool found = false;
                    foreach (var cell in row.CellsUsed())
                    {
                        var text = cell.Value.ToString().Trim();
                        if (text.Equals(ExcelColumnMap.ConsumerName, StringComparison.OrdinalIgnoreCase) ||
                            text.Equals(ExcelColumnMap.CurrentNewMemberCode, StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found)
                    {
                        headerRow = row;
                        break;
                    }

                    // Collect header key-value pairs from pre-data rows
                    var matchedCells = new List<(IXLCell cell, string matchedKey)>();
                    foreach (var cell in row.CellsUsed())
                    {
                        var text = cell.Value.ToString().Trim();
                        if (knownHeaderKeys.Contains(text)) matchedCells.Add((cell, text));
                    }

                    if (matchedCells.Count >= 2)
                    {
                        var nextRow = worksheet.Row(r + 1);
                        foreach (var (headerCell, key) in matchedCells)
                        {
                            int colNum = headerCell.Address.ColumnNumber;
                            var valCell = nextRow.Cell(colNum);
                            var val = ReadCellAsString(valCell);
                            if (!string.IsNullOrEmpty(val)) result.HeaderData[key] = val;
                        }
                        skipNextRowsCount = 1;
                    }
                    else if (matchedCells.Count == 1)
                    {
                        var (keyCell, key) = matchedCells[0];
                        int colNum = keyCell.Address.ColumnNumber;
                        var valCell = row.Cell(colNum + 1);
                        var val = ReadCellAsString(valCell);
                        if (!string.IsNullOrEmpty(val)) result.HeaderData[key] = val;
                    }
                    else
                    {
                        var cellsUsed = row.CellsUsed().ToList();
                        if (cellsUsed.Count >= 2)
                        {
                            var key = cellsUsed[0].Value.ToString().Trim();
                            var val = ReadCellAsString(cellsUsed[1]);
                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                                result.HeaderData[key] = val;
                        }
                    }
                }

                if (headerRow == null)
                {
                    result.Errors.Add("Could not locate the header row in the template. Expected to find 'Consumer Name' or 'Current/New Member Code'.");
                    return result;
                }

                var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in headerRow.CellsUsed())
                {
                    var header = cell.Value.ToString().Trim();
                    if (!string.IsNullOrEmpty(header) && !columnMap.ContainsKey(header))
                        columnMap[header] = cell.Address.ColumnNumber;
                }

                var expectedColumns = ExcelColumnMap.GetAllExpectedColumns();
                var missingColumns = expectedColumns.Where(c => !columnMap.ContainsKey(c)).ToList();

                if (missingColumns.Any())
                {
                    result.Errors.Add($"Missing required columns in template: {string.Join(", ", missingColumns)}");
                    return result;
                }

                var rowsUsed = worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber());

                foreach (var row in rowsUsed)
                {
                    bool isEmpty = true;
                    foreach (var cell in row.CellsUsed())
                    {
                        if (!string.IsNullOrWhiteSpace(cell.Value.ToString()))
                        {
                            isEmpty = false;
                            break;
                        }
                    }

                    if (isEmpty) continue;

                    var rawRow = new RawExcelRow { RowNumber = row.RowNumber() };

                    foreach (var kvp in columnMap)
                    {
                        var cell = row.Cell(kvp.Value);
                        rawRow.Columns[kvp.Key] = ReadCellAsString(cell);
                    }

                    result.Rows.Add(rawRow);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error reading Excel file: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Read a cell value as a string, handling date and number types correctly.
        /// Dates are formatted as ddMMyyyy (UCRF standard).
        /// </summary>
        private static string ReadCellAsString(IXLCell cell)
        {
            if (cell.DataType == XLDataType.DateTime && cell.Value.IsDateTime)
                return cell.Value.GetDateTime().ToString("ddMMyyyy");
            if (cell.DataType == XLDataType.Number && cell.Value.IsNumber)
                return cell.Value.GetNumber().ToString("0.################");
            return cell.Value.ToString().Trim();
        }
    }
}
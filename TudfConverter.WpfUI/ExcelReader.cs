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
                    DateOfBirth = TryParseDate(GetValue(row, ExcelColumnMap.DateOfBirth), out var dob) ? dob : null,
                    Gender = int.TryParse(GetValue(row, ExcelColumnMap.Gender), out int g) ? g : null
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
                DateOfLastPayment = TryParseDate(GetValue(row, ExcelColumnMap.DateOfLastPayment), out var d2) ? d2 : null,
                DateClosed = TryParseDate(GetValue(row, ExcelColumnMap.DateClosed), out var d3) ? d3 : null,
                DateReportedAndCertified = TryParseDate(GetValue(row, ExcelColumnMap.DateReportedAndCertified), out var d4) ? d4 : DateTime.Now,
                HighCreditSanctionedAmount = TryParseLong(GetValue(row, ExcelColumnMap.HighCreditSanctionedAmt), out var hc) ? hc : 0,
                CurrentBalance = TryParseLong(GetValue(row, ExcelColumnMap.CurrentBalance), out var bal) ? bal : 0,
                IsCurrentBalanceNegative = (GetValue(row, ExcelColumnMap.CurrentBalance) ?? "").Trim().StartsWith("-"),
                AmountOverdue = TryParseLong(GetValue(row, ExcelColumnMap.AmtOverdue), out var over) ? over : null,
                NumberOfDaysPastDue = int.TryParse(GetValue(row, ExcelColumnMap.NoOfDaysPastDue), out int past) ? past : null,
                OldReportingMemberCode = GetValue(row, ExcelColumnMap.OldMbrCode),
                OldMemberShortName = GetValue(row, ExcelColumnMap.OldMbrShortName),
                OldAccountNumber = GetValue(row, ExcelColumnMap.OldAccNo),
                OldAccountType = int.TryParse(GetValue(row, ExcelColumnMap.OldAccType), out int oldtype) ? oldtype : null,
                OldOwnershipIndicator = int.TryParse(GetValue(row, ExcelColumnMap.OldOwnershipIndicator), out int oldown) ? oldown : null,
                SuitFiledWilfulDefault = int.TryParse(GetValue(row, ExcelColumnMap.SuitFiledWilfulDefault), out int suit) ? suit : null,
                AssetClassification = int.TryParse(GetValue(row, ExcelColumnMap.AssetClassification), out int asset) ? asset : null,
                ValueOfCollateral = TryParseLong(GetValue(row, ExcelColumnMap.ValueOfCollateral), out var coll) ? coll : null,
                TypeOfCollateral = int.TryParse(GetValue(row, ExcelColumnMap.TypeOfCollateral), out int coltype) ? coltype : null,
                CreditLimit = TryParseLong(GetValue(row, ExcelColumnMap.CreditLimit), out var clim) ? clim : null,
                CashLimit = TryParseLong(GetValue(row, ExcelColumnMap.CashLimit), out var cashlim) ? cashlim : null,
                RateOfInterest = GetValue(row, ExcelColumnMap.RateOfInterest),
                RepaymentTenure = int.TryParse(GetValue(row, ExcelColumnMap.RepaymentTenure), out int tenure) ? tenure : null,
                EmiAmount = TryParseLong(GetValue(row, ExcelColumnMap.EmiAmount), out var emi) ? emi : null,
                WrittenOffAmountTotal = TryParseLong(GetValue(row, ExcelColumnMap.WrittenOffAmountTotal), out var woTot) ? woTot : null,
                WrittenOffAmountPrincipal = TryParseLong(GetValue(row, ExcelColumnMap.WrittenOffPrincipalAmount), out var woPrin) ? woPrin : null,
                SettlementAmount = TryParseLong(GetValue(row, ExcelColumnMap.SettlementAmt), out var setl) ? setl : null,
                PaymentFrequency = int.TryParse(GetValue(row, ExcelColumnMap.PaymentFrequency), out int freq) ? freq : null,
                ActualPaymentAmount = TryParseLong(GetValue(row, ExcelColumnMap.ActualPaymentAmt), out var act) ? act : null,
                OccupationCode = int.TryParse(GetValue(row, ExcelColumnMap.OccupationCode), out int occ) ? occ : null,
                Income = TryParseLong(GetValue(row, ExcelColumnMap.Income), out var inc) ? inc : null,
                NetGrossIncomeIndicator = GetValue(row, ExcelColumnMap.NetGrossIncomeIndicator),
                MonthlyAnnualIncomeIndicator = GetValue(row, ExcelColumnMap.MonthlyAnnualIncomeIndicator),
                CreditFacilityStatus = int.TryParse(GetValue(row, ExcelColumnMap.CreditFacilityStatus), out int cfs) ? cfs : null
            };

            return model;
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
                    // If single word longer than maxLen, truncate
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

                // Remove hyphens from address string, then split into max 40-char lines
                var cleanAddress = address1Raw.Replace("-", "");
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

            AddId(ExcelColumnMap.IncomeTaxIdNumber, 1);
            AddId(ExcelColumnMap.PassportNumber, 2, ExcelColumnMap.PassportIssueDate, ExcelColumnMap.PassportExpiryDate);
            AddId(ExcelColumnMap.VoterIdNumber, 3);
            AddId(ExcelColumnMap.DrivingLicenseNumber, 4, ExcelColumnMap.DrivingLicenseIssueDate, ExcelColumnMap.DrivingLicenseExpiryDate);
            AddId(ExcelColumnMap.RationCardNumber, 5);
            AddId(ExcelColumnMap.UniversalIdNumber, 6);

            var ckycVal = GetValue(row, ExcelColumnMap.Ckyc);
            if (!string.IsNullOrWhiteSpace(ckycVal)) AddId(ExcelColumnMap.Ckyc, 9);
            else AddId(ExcelColumnMap.AdditionalId1, 9);

            var nregaVal = GetValue(row, ExcelColumnMap.NregaCardNumber);
            if (!string.IsNullOrWhiteSpace(nregaVal)) AddId(ExcelColumnMap.NregaCardNumber, 10);
            else AddId(ExcelColumnMap.AdditionalId2, 10);

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

            AddPhone(ExcelColumnMap.TelephoneNoMobile, "01");
            AddPhone(ExcelColumnMap.TelephoneNoResidence, "02");
            AddPhone(ExcelColumnMap.TelephoneNoOffice, "03", ExcelColumnMap.ExtensionOffice);
            AddPhone(ExcelColumnMap.TelephoneNoOther, "04", ExcelColumnMap.ExtensionOther);

            return phones;
        }

        private List<EmailModel> MapEmails(RawExcelRow row)
        {
            var emails = new List<EmailModel>();
            var email1 = GetValue(row, ExcelColumnMap.EmailId1);
            if (!string.IsNullOrWhiteSpace(email1)) emails.Add(new EmailModel { SegmentIndex = 1, EmailId = email1 });
            return emails;
        }

        private List<AccountHistoryModel> MapAccountHistory(RawExcelRow row)
        {
            var history = new List<AccountHistoryModel>();
            int index = 1;

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

        private static bool TryParseDate(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (DateTime.TryParseExact(value, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out date)) return true;
            if (DateTime.TryParseExact(value, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out date)) return true;
            if (DateTime.TryParseExact(value, "ddMMyyyy", null, System.Globalization.DateTimeStyles.None, out date)) return true;
            if (DateTime.TryParse(value, out var dt))
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
                var knownHeaderKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name of the CU", "Member UserId", "Cycle Date", "Cycle Identifier" };
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
                            var val = valCell.Value.ToString().Trim();
                            if (valCell.DataType == XLDataType.DateTime && valCell.Value.IsDateTime)
                                val = valCell.Value.GetDateTime().ToString("ddMMyyyy");
                            if (!string.IsNullOrEmpty(val)) result.HeaderData[key] = val;
                        }
                        skipNextRowsCount = 1;
                    }
                    else if (matchedCells.Count == 1)
                    {
                        var (keyCell, key) = matchedCells[0];
                        int colNum = keyCell.Address.ColumnNumber;
                        var valCell = row.Cell(colNum + 1);
                        var val = valCell.Value.ToString().Trim();
                        if (valCell.DataType == XLDataType.DateTime && valCell.Value.IsDateTime)
                            val = valCell.Value.GetDateTime().ToString("ddMMyyyy");
                        if (!string.IsNullOrEmpty(val)) result.HeaderData[key] = val;
                    }
                    else
                    {
                        var cellsUsed = row.CellsUsed().ToList();
                        if (cellsUsed.Count >= 2)
                        {
                            var key = cellsUsed[0].Value.ToString().Trim();
                            var val = cellsUsed[1].Value.ToString().Trim();
                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                            {
                                if (cellsUsed[1].DataType == XLDataType.DateTime && cellsUsed[1].Value.IsDateTime)
                                    val = cellsUsed[1].Value.GetDateTime().ToString("ddMMyyyy");
                                result.HeaderData[key] = val;
                            }
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
                    if (!string.IsNullOrEmpty(header)) columnMap[header] = cell.Address.ColumnNumber;
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
                        string cellValue = string.Empty;

                        if (cell.DataType == XLDataType.DateTime && cell.Value.IsDateTime)
                            cellValue = cell.Value.GetDateTime().ToString("dd/MM/yyyy");
                        else if (cell.DataType == XLDataType.Number && cell.Value.IsNumber)
                            cellValue = cell.Value.GetNumber().ToString("0.################");
                        else
                            cellValue = cell.Value.ToString().Trim();

                        rawRow.Columns[kvp.Key] = cellValue;
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
    }
}
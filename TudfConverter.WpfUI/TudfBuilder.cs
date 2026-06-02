using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace TudfConverter.WpfUI
{
    public static class TudfFieldFormatter
    {
        /// <summary>
        /// Format a variable-length field: tag(2) + len(2) + value
        /// Returns empty string if value is null or empty.
        /// </summary>
        public static string FormatVariableField(string tag, string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var len = value.Length.ToString("D2");
            return $"{tag}{len}{value}";
        }

        public static string FormatNumericVariableField(string tag, long? value)
        {
            if (!value.HasValue) return string.Empty;
            return FormatVariableField(tag, value.Value.ToString());
        }

        /// <summary>
        /// Format signed amount: amount string followed by '-' if negative, nothing if positive.
        /// Per spec: sign appears at the RIGHTMOST position.
        /// </summary>
        public static string FormatSignedAmountField(string tag, long amount, bool isNegative)
        {
            var amountStr = Math.Abs(amount).ToString();
            if (isNegative) amountStr += "-";
            return FormatVariableField(tag, amountStr);
        }

        /// <summary>
        /// Format fixed-length alpha/alphanumeric field: left-justified, space-padded.
        /// </summary>
        public static string FormatFixedAlpha(string? value, int length)
        {
            var v = value ?? string.Empty;
            if (v.Length > length) v = v.Substring(0, length);
            return v.PadRight(length);
        }

        /// <summary>
        /// Format fixed-length numeric field: right-justified, zero-padded.
        /// </summary>
        public static string FormatFixedNumeric(string? value, int length)
        {
            var v = value ?? string.Empty;
            // Keep only digits
            v = new string(v.Where(char.IsDigit).ToArray());
            if (v.Length > length) v = v.Substring(v.Length - length);
            return v.PadLeft(length, '0');
        }

        /// <summary>
        /// Format date as ddMMyyyy (8 chars).
        /// </summary>
        public static string FormatDate(DateTime? date)
        {
            if (!date.HasValue) return string.Empty;
            return date.Value.ToString("ddMMyyyy");
        }

        public static string FormatVariableDateField(string tag, DateTime? date)
        {
            if (!date.HasValue) return string.Empty;
            return FormatVariableField(tag, FormatDate(date));
        }

        public static string FormatRateOfInterest(string tag, string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var len = value.Length.ToString("D2");
            return $"{tag}{len}{value}";
        }

        public static string NormalizeAddressLine(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Remove hyphens only — slashes and spaces are preserved
            var result = input.Replace("-", "");

            // Collapse multiple spaces into one
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();

            return result;
        }
    }

    public class TudfSegmentBuilder
    {
        private static readonly int[] CreditCardAccountTypes = { 10, 16, 31, 35 };

        /// <summary>
        /// Build the 146-byte fixed-length Header Segment (HD).
        /// Layout per UCRF v3.74:
        ///   Pos  1- 4:  Segment Tag       (4)  "TUDF" or "CONS"
        ///   Pos  5- 6:  Version           (2)  "12"
        ///   Pos  7-36:  Member User ID    (30) left-justified, space-padded
        ///   Pos 37-52:  Short Name        (16) left-justified, space-padded (optional)
        ///   Pos 53-54:  Reporting Cycle   (2)  left-justified, space-padded
        ///   Pos 55-62:  Date Reported     (8)  ddMMyyyy
        ///   Pos 63-92:  Future Use 1      (30) spaces
        ///   Pos 93-93:  Future Use 2      (1)  space (was "A" in old spec, now future use)
        ///   Pos 94-98:  Future Use 3      (5)  "00000" or spaces
        ///   Pos 99-146: Member Data       (48) spaces
        /// Total: 4+2+30+16+2+8+30+1+5+48 = 146
        /// </summary>
        public string BuildHeader(HeaderSegmentModel header)
        {
            var sb = new StringBuilder(146);
            sb.Append("TUDF");
            sb.Append("12");
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.MemberUserId, 30));
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.ShortName, 16));
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.ReportingCycle, 2));
            sb.Append(header.DateReportedAndCertified.ToString("ddMMyyyy"));
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.FutureUse1, 30));
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.FutureUse2, 1));
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.FutureUse3, 5));
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.MemberData, 48));

            var result = sb.ToString();
            if (result.Length != 146)
                throw new InvalidOperationException(
                    $"HD segment length {result.Length} does not equal required 146 bytes.");
            return result;
        }

        /// <summary>
        /// Build the Name Segment (PN).
        /// Format: PN + 03 + N01 + [field tags]
        /// Consumer name is split into words and packed into up to 5 name fields,
        /// each max 26 chars in the value.
        /// Name words must NOT be split across fields.
        /// </summary>
        public string BuildName(NameSegmentModel name)
        {
            var sb = new StringBuilder();
            sb.Append("PN");
            sb.Append("03");
            sb.Append("N01");

            var words = (name.FullName ?? string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var packedTags = new List<string>();
            int wordIndex = 0;
            const int maxFieldLen = 26;

            while (wordIndex < words.Length && packedTags.Count < 5)
            {
                var word = words[wordIndex].Length > maxFieldLen
                    ? words[wordIndex].Substring(0, maxFieldLen)
                    : words[wordIndex];
                var current = word;
                wordIndex++;

                while (wordIndex < words.Length)
                {
                    var nextWord = words[wordIndex];
                    if (current.Length + 1 + nextWord.Length <= maxFieldLen)
                    {
                        current += " " + nextWord;
                        wordIndex++;
                    }
                    else break;
                }
                packedTags.Add(current);
            }

            for (int i = 0; i < packedTags.Count; i++)
            {
                var tag = (i + 1).ToString("D2");
                sb.Append(TudfFieldFormatter.FormatVariableField(tag, packedTags[i]));
            }

            // Field 07: Date of Birth (ddMMyyyy, 8 chars fixed)
            if (name.DateOfBirth.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("07", name.DateOfBirth));

            // Field 08: Gender (1 char: 1=Female, 2=Male, 3=Transgender)
            if (name.Gender.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("08", name.Gender.Value.ToString()));

            return sb.ToString();
        }

        /// <summary>
        /// Build an Identification Segment (ID).
        /// Format: ID + 03 + I[nn] + [fields]
        /// </summary>
        public string BuildIdentification(IdentificationModel id)
        {
            var sb = new StringBuilder();
            var segTag = "I" + id.SegmentIndex.ToString("D2");
            sb.Append("ID");
            sb.Append("03");
            sb.Append(segTag);

            sb.Append(TudfFieldFormatter.FormatVariableField("01", id.IdType.ToString("D2")));
            sb.Append(TudfFieldFormatter.FormatVariableField("02", id.IdNumber));

            if (id.IssueDate.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("03", id.IssueDate));
            if (id.ExpirationDate.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("04", id.ExpirationDate));

            return sb.ToString();
        }

        /// <summary>
        /// Build a Telephone Segment (PT).
        /// Format: PT + 03 + T[nn] + [fields]
        /// Telephone types: 00=Not Classified, 01=Mobile, 02=Home, 03=Office
        /// </summary>
        public string BuildTelephone(TelephoneModel phone)
        {
            var sb = new StringBuilder();
            var segTag = "T" + phone.SegmentIndex.ToString("D2");
            sb.Append("PT");
            sb.Append("03");
            sb.Append(segTag);

            sb.Append(TudfFieldFormatter.FormatVariableField("01", phone.TelephoneNumber));

            if (!string.IsNullOrEmpty(phone.TelephoneExtension))
                sb.Append(TudfFieldFormatter.FormatVariableField("02", phone.TelephoneExtension));

            sb.Append(TudfFieldFormatter.FormatVariableField("03", phone.TelephoneType));

            return sb.ToString();
        }

        /// <summary>
        /// Build an Email Contact Segment (EC).
        /// Format: EC + 03 + C[nn] + [fields]
        /// </summary>
        public string BuildEmail(EmailModel email)
        {
            var sb = new StringBuilder();
            var segTag = "C" + email.SegmentIndex.ToString("D2");
            sb.Append("EC");
            sb.Append("03");
            sb.Append(segTag);
            sb.Append(TudfFieldFormatter.FormatVariableField("01", email.EmailId));
            return sb.ToString();
        }

        /// <summary>
        /// Build an Address Segment (PA).
        /// Format: PA + 03 + A[nn] + [fields]
        ///
        /// Address normalization rules:
        ///   - Remove hyphens and spaces (words are concatenated per TUDF spec)
        ///   - Preserve forward slashes (e.g. AT/POST, BK/TAL)
        ///   - Maximum 40 chars per address line value
        /// </summary>
        public string BuildAddress(AddressModel address)
        {
            var sb = new StringBuilder();
            var segTag = "A" + address.SegmentIndex.ToString("D2");

            sb.Append("PA");
            sb.Append("03");
            sb.Append(segTag);

            // Address Line 1
            if (!string.IsNullOrEmpty(address.AddressLine1))
            {
                var line = TudfFieldFormatter.NormalizeAddressLine(address.AddressLine1);
                sb.Append(TudfFieldFormatter.FormatVariableField("01", line));
            }

            // Address Line 2
            if (!string.IsNullOrEmpty(address.AddressLine2))
            {
                var line = TudfFieldFormatter.NormalizeAddressLine(address.AddressLine2);
                sb.Append(TudfFieldFormatter.FormatVariableField("02", line));
            }

            // Address Line 3
            if (!string.IsNullOrEmpty(address.AddressLine3))
            {
                var line = TudfFieldFormatter.NormalizeAddressLine(address.AddressLine3);
                sb.Append(TudfFieldFormatter.FormatVariableField("03", line));
            }

            // Address Line 4
            if (!string.IsNullOrEmpty(address.AddressLine4))
            {
                var line = TudfFieldFormatter.NormalizeAddressLine(address.AddressLine4);
                sb.Append(TudfFieldFormatter.FormatVariableField("04", line));
            }

            // Address Line 5
            if (!string.IsNullOrEmpty(address.AddressLine5))
            {
                var line = TudfFieldFormatter.NormalizeAddressLine(address.AddressLine5);
                sb.Append(TudfFieldFormatter.FormatVariableField("05", line));
            }

            // State Code
            if (!string.IsNullOrEmpty(address.StateCode))
            {
                sb.Append(
                    TudfFieldFormatter.FormatVariableField(
                        "06",
                        TudfFieldFormatter.FormatFixedNumeric(address.StateCode, 2)
                    ));
            }

            // PIN Code — extract exactly 6 digits
            if (!string.IsNullOrEmpty(address.PinCode))
            {
                var digitsOnly = new string(address.PinCode.Where(char.IsDigit).ToArray());

                if (digitsOnly.Length >= 6)
                {
                    var pinFormatted = digitsOnly.Length > 6
                        ? digitsOnly.Substring(digitsOnly.Length - 6)
                        : digitsOnly;

                    sb.Append(TudfFieldFormatter.FormatVariableField("07", pinFormatted));
                }
            }

            // Address Category (default = 04 if not provided)
            var addrCat = address.AddressCategory.HasValue
                ? address.AddressCategory.Value.ToString("D2")
                : "04";
            sb.Append(TudfFieldFormatter.FormatVariableField("08", addrCat));

            // Residence Code
            if (address.ResidenceCode.HasValue)
            {
                sb.Append(
                    TudfFieldFormatter.FormatVariableField(
                        "09",
                        address.ResidenceCode.Value.ToString("D2")
                    ));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Build the Account Segment (TL).
        /// Format: TL + 04 + T001 + [fields]
        /// Tags 06, 07, 23-25, 27-33 are reserved — skipped.
        /// </summary>
        public string BuildAccount(AccountSegmentModel account)
        {
            var sb = new StringBuilder();
            sb.Append("TL");
            sb.Append("04");
            sb.Append("T001");

            bool isCC = CreditCardAccountTypes.Contains(account.AccountType);

            sb.Append(TudfFieldFormatter.FormatVariableField("01", account.CurrentMemberCode));

            if (!string.IsNullOrEmpty(account.MemberShortName))
                sb.Append(TudfFieldFormatter.FormatVariableField("02", account.MemberShortName));

            sb.Append(TudfFieldFormatter.FormatVariableField("03", account.AccountNumber));
            sb.Append(TudfFieldFormatter.FormatVariableField("04", account.AccountType.ToString("D2")));
            sb.Append(TudfFieldFormatter.FormatVariableField("05", account.OwnershipIndicator.ToString()));

            // Tags 06, 07: RESERVED — skip

            sb.Append(TudfFieldFormatter.FormatVariableDateField("08", account.DateOpenedDisbursed));

            if (account.DateOfLastPayment.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("09", account.DateOfLastPayment));
            if (account.DateClosed.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("10", account.DateClosed));

            sb.Append(TudfFieldFormatter.FormatVariableDateField("11", account.DateReportedAndCertified));
            sb.Append(TudfFieldFormatter.FormatNumericVariableField("12", account.HighCreditSanctionedAmount));

            if (isCC && account.CurrentBalance == 0)
                sb.Append(TudfFieldFormatter.FormatVariableField("13", "0"));
            else
                sb.Append(TudfFieldFormatter.FormatSignedAmountField("13",
                    account.CurrentBalance, account.IsCurrentBalanceNegative));

            if (account.AmountOverdue.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("14", account.AmountOverdue));

            if (account.NumberOfDaysPastDue.HasValue)
            {
                var ndpd = account.NumberOfDaysPastDue.Value;
                if (ndpd > 900) ndpd = 900;
                var ndpdStr = ndpd == 0 ? "0" : ndpd.ToString("D3");
                sb.Append(TudfFieldFormatter.FormatVariableField("15", ndpdStr));
            }
            else if (account.AmountOverdue.HasValue && account.AmountOverdue > 0 && isCC)
            {
                sb.Append(TudfFieldFormatter.FormatVariableField("15", "000"));
            }

            if (!string.IsNullOrEmpty(account.OldReportingMemberCode))
                sb.Append(TudfFieldFormatter.FormatVariableField("16", account.OldReportingMemberCode));
            if (!string.IsNullOrEmpty(account.OldMemberShortName))
                sb.Append(TudfFieldFormatter.FormatVariableField("17", account.OldMemberShortName));
            if (!string.IsNullOrEmpty(account.OldAccountNumber))
                sb.Append(TudfFieldFormatter.FormatVariableField("18", account.OldAccountNumber));
            if (account.OldAccountType.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("19",
                    account.OldAccountType.Value.ToString("D2")));
            if (account.OldOwnershipIndicator.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("20",
                    account.OldOwnershipIndicator.Value.ToString("D2")));

            if (account.SuitFiledWilfulDefault.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("21",
                    account.SuitFiledWilfulDefault.Value.ToString("D2")));

            if (account.CreditFacilityStatus.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("22",
                    account.CreditFacilityStatus.Value.ToString("D2")));

            // Tags 23-25: RESERVED — skip

            if (account.AssetClassification.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("26",
                    account.AssetClassification.Value.ToString("D2")));

            // Tags 27-33: RESERVED — skip

            if (account.ValueOfCollateral.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("34", account.ValueOfCollateral));
            if (account.TypeOfCollateral.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("35",
                    account.TypeOfCollateral.Value.ToString("D2")));

            if (account.CreditLimit.HasValue && isCC)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("36", account.CreditLimit));
            if (account.CashLimit.HasValue && isCC)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("37", account.CashLimit));

            if (!string.IsNullOrEmpty(account.RateOfInterest))
            {
                var roi = TudfFieldFormatter.FormatRateOfInterest("38", account.RateOfInterest);
                if (!string.IsNullOrEmpty(roi))
                    sb.Append(roi);
            }

            if (account.RepaymentTenure.HasValue && account.RepaymentTenure.Value > 0)
                sb.Append(TudfFieldFormatter.FormatVariableField("39",
                    account.RepaymentTenure.Value.ToString("D3")));

            var noEmiTypes = new[] { 10, 12, 16, 31, 35 };
            if (account.EmiAmount.HasValue && account.EmiAmount.Value > 0 &&
                !noEmiTypes.Contains(account.AccountType))
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("40", account.EmiAmount));

            if (account.WrittenOffAmountTotal.HasValue && account.WrittenOffAmountTotal.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("41", account.WrittenOffAmountTotal));
            if (account.WrittenOffAmountPrincipal.HasValue && account.WrittenOffAmountPrincipal.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("42", account.WrittenOffAmountPrincipal));
            if (account.SettlementAmount.HasValue && account.SettlementAmount.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("43", account.SettlementAmount));

            if (account.PaymentFrequency.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("44",
                    account.PaymentFrequency.Value.ToString("D2")));
            if (account.ActualPaymentAmount.HasValue && account.ActualPaymentAmount.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("45", account.ActualPaymentAmount));

            if (account.OccupationCode.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("46",
                    account.OccupationCode.Value.ToString("D2")));
            if (account.Income.HasValue && account.Income.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("47", account.Income));
            if (!string.IsNullOrEmpty(account.NetGrossIncomeIndicator))
                sb.Append(TudfFieldFormatter.FormatVariableField("48", account.NetGrossIncomeIndicator));
            if (!string.IsNullOrEmpty(account.MonthlyAnnualIncomeIndicator))
                sb.Append(TudfFieldFormatter.FormatVariableField("49", account.MonthlyAnnualIncomeIndicator));

            return sb.ToString();
        }

        /// <summary>
        /// Build an Account History Segment (TH).
        /// Format: TH + 03 + H[nn] + [fields]
        /// Max 47 occurrences (H01–H47).
        /// Field 01 (Account History Date): day always reset to 01.
        /// Field 02 (Asset Classification / NDPD): STD/SUB/DBT/LSS/SMA or 3-digit number.
        /// </summary>
        public string BuildAccountHistory(AccountHistoryModel history)
        {
            var sb = new StringBuilder();
            var segTag = "H" + history.SegmentIndex.ToString("D2");

            sb.Append("TH");
            sb.Append("03");
            sb.Append(segTag);

            var histDate = history.AccountHistoryDate != default
                ? new DateTime(history.AccountHistoryDate.Year, history.AccountHistoryDate.Month, 1)
                : default;
            sb.Append(TudfFieldFormatter.FormatVariableField("01",
                TudfFieldFormatter.FormatDate(histDate)));

            var ndpdVal = (history.AssetClassificationNdpd ?? "").Trim().ToUpperInvariant();
            var validTextValues = new HashSet<string> { "STD", "SUB", "DBT", "LSS", "SMA" };
            string ndpdFormatted;
            if (validTextValues.Contains(ndpdVal))
            {
                ndpdFormatted = ndpdVal;
            }
            else if (int.TryParse(ndpdVal, out int ndpdNum))
            {
                if (ndpdNum > 900) ndpdNum = 900;
                ndpdFormatted = ndpdNum.ToString("D3");
            }
            else
            {
                ndpdFormatted = "000";
            }
            sb.Append(TudfFieldFormatter.FormatVariableField("02", ndpdFormatted));

            if (history.AmountOverdue.HasValue && history.AmountOverdue.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("03", history.AmountOverdue));
            if (history.HighCreditSanctionedAmount.HasValue && history.HighCreditSanctionedAmount.Value >= 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("04", history.HighCreditSanctionedAmount));
            if (history.CreditLimit.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("05", history.CreditLimit));
            if (history.CashLimit.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("06", history.CashLimit));

            sb.Append(TudfFieldFormatter.FormatSignedAmountField("07",
                history.CurrentBalance, history.IsCurrentBalanceNegative));

            if (history.DateOfLastPayment.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("08", history.DateOfLastPayment));
            if (history.ActualPaymentAmount.HasValue && history.ActualPaymentAmount.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("09", history.ActualPaymentAmount));

            return sb.ToString();
        }

        /// <summary>
        /// Build the End of Subject Segment (ES).
        /// Fixed 6 bytes: "ES02**"
        /// </summary>
        public string BuildEndSegment() => "ES02**";

        /// <summary>
        /// Build the Trailer Segment (TRLR).
        /// Fixed 4 bytes: "TRLR"
        /// Must appear exactly once at the end of the file.
        /// </summary>
        public string BuildTrailerSegment() => "TRLR";
    }

    public class TudfGeneratorService
    {
        /// <summary>
        /// Generate the complete TUDF file content.
        /// Segment order per spec: HD, then for each record: PN, ID, PT, EC, PA, TL, TH, ES, then TRLR.
        /// </summary>
        public string Generate(List<CustomerRecord> records, HeaderSegmentModel header)
        {
            var sb = new StringBuilder();
            var builder = new TudfSegmentBuilder();

            sb.Append(builder.BuildHeader(header));

            foreach (var record in records)
            {
                sb.Append(builder.BuildName(record.Name));

                foreach (var id in record.Identifications)
                    sb.Append(builder.BuildIdentification(id));

                foreach (var phone in record.Telephones)
                    sb.Append(builder.BuildTelephone(phone));

                foreach (var email in record.Emails)
                    sb.Append(builder.BuildEmail(email));

                foreach (var address in record.Addresses)
                    sb.Append(builder.BuildAddress(address));

                sb.Append(builder.BuildAccount(record.Account));

                foreach (var history in record.AccountHistory)
                    sb.Append(builder.BuildAccountHistory(history));

                sb.Append(builder.BuildEndSegment());
            }

            sb.Append(builder.BuildTrailerSegment());

            return sb.ToString();
        }
    }
}
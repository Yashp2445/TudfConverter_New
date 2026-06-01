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
            var amountStr = amount.ToString();
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

        /// <summary>
        /// Format Rate of Interest: numeric decimal value, max 4 digits before decimal, 3 after.
        /// </summary>
        public static string FormatRateOfInterest(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            // Try to parse as decimal to normalize
            if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var rate))
            {
                // Per spec: max 4 digits before decimal, max 3 after, no % sign
                // Format as up to 4.3 decimal representation
                var parts = rate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture).Split('.');
                var intPart = parts[0];
                var decPart = parts.Length > 1 ? parts[1] : "";
                if (intPart.Length > 4) return string.Empty; // invalid per spec
                if (rate == 0m) return string.Empty; // 0.0 is rejected per spec
                return decPart.Length > 0 ? $"{intPart}.{decPart}" : intPart;
            }
            return value.Trim();
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
            sb.Append("TUDF");                                                          // 4
            sb.Append("12");                                                             // 2
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.MemberUserId, 30));    // 30
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.ShortName, 16));       // 16
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.ReportingCycle, 2));   // 2
            sb.Append(header.DateReportedAndCertified.ToString("ddMMyyyy"));            // 8
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.FutureUse1, 30));      // 30 (spaces)
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.FutureUse2, 1));       // 1  (space)
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.FutureUse3, 5));       // 5  ("00000")
            sb.Append(TudfFieldFormatter.FormatFixedAlpha(header.MemberData, 48));      // 48 (spaces)

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
        /// each max 26 bytes (tag=2 + len=2 + value=max 22 = 26 encoded, value limit = 26 per spec).
        /// Actually per spec: field max length = 26 bytes for the value, but field tag takes
        /// 4 bytes overhead, so spec says max 26 chars in the name field VALUE.
        /// Per spec example: "0111HAREN PATEL" where 01=tag, 11=len, value=11 chars
        /// Field value max = 26 chars per field.
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
            const int maxFieldLen = 26; // per spec

            while (wordIndex < words.Length && packedTags.Count < 5)
            {
                // Truncate single word if > maxFieldLen
                var word = words[wordIndex].Length > maxFieldLen
                    ? words[wordIndex].Substring(0, maxFieldLen)
                    : words[wordIndex];
                var current = word;
                wordIndex++;

                // Pack additional words into this field if they fit
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

            // Field 01: ID Type (2-digit zero-padded)
            sb.Append(TudfFieldFormatter.FormatVariableField("01", id.IdType.ToString("D2")));
            // Field 02: ID Number
            sb.Append(TudfFieldFormatter.FormatVariableField("02", id.IdNumber));
            // Field 03: Issue Date (optional)
            if (id.IssueDate.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("03", id.IssueDate));
            // Field 04: Expiration Date (optional)
            if (id.ExpirationDate.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("04", id.ExpirationDate));

            return sb.ToString();
        }

        /// <summary>
        /// Build a Telephone Segment (PT).
        /// Format: PT + 03 + T[nn] + [fields]
        /// Telephone types per v3.74: 00=Not Classified, 01=Mobile, 02=Home, 03=Office
        /// </summary>
        public string BuildTelephone(TelephoneModel phone)
        {
            var sb = new StringBuilder();
            var segTag = "T" + phone.SegmentIndex.ToString("D2");
            sb.Append("PT");
            sb.Append("03");
            sb.Append(segTag);

            // Field 01: Telephone Number
            sb.Append(TudfFieldFormatter.FormatVariableField("01", phone.TelephoneNumber));
            // Field 02: Extension (optional)
            if (!string.IsNullOrEmpty(phone.TelephoneExtension))
                sb.Append(TudfFieldFormatter.FormatVariableField("02", phone.TelephoneExtension));
            // Field 03: Telephone Type (2-char fixed)
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
        /// </summary>
        public string BuildAddress(AddressModel address)
        {
            var sb = new StringBuilder();
            var segTag = "A" + address.SegmentIndex.ToString("D2");
            sb.Append("PA");
            sb.Append("03");
            sb.Append(segTag);

            // Fields 01-05: Address Lines (max 40 chars each)
            sb.Append(TudfFieldFormatter.FormatVariableField("01", address.AddressLine1));
            if (!string.IsNullOrEmpty(address.AddressLine2))
                sb.Append(TudfFieldFormatter.FormatVariableField("02", address.AddressLine2));
            if (!string.IsNullOrEmpty(address.AddressLine3))
                sb.Append(TudfFieldFormatter.FormatVariableField("03", address.AddressLine3));
            if (!string.IsNullOrEmpty(address.AddressLine4))
                sb.Append(TudfFieldFormatter.FormatVariableField("04", address.AddressLine4));
            if (!string.IsNullOrEmpty(address.AddressLine5))
                sb.Append(TudfFieldFormatter.FormatVariableField("05", address.AddressLine5));

            // Field 06: State Code (2-digit zero-padded)
            if (!string.IsNullOrEmpty(address.StateCode))
                sb.Append(TudfFieldFormatter.FormatVariableField("06",
                    TudfFieldFormatter.FormatFixedNumeric(address.StateCode, 2)));

            // Field 07: PIN Code (6 digits, stripped of non-digits)
            if (!string.IsNullOrEmpty(address.PinCode))
            {
                var digitsOnly = new string(address.PinCode.Where(char.IsDigit).ToArray());
                if (digitsOnly.Length >= 6)
                {
                    // Keep last 6 digits if longer; take first 6 if exactly 6
                    var pinFormatted = digitsOnly.Length > 6
                        ? digitsOnly.Substring(digitsOnly.Length - 6)
                        : digitsOnly;
                    sb.Append(TudfFieldFormatter.FormatVariableField("07", pinFormatted));
                }
            }

            // Field 08: Address Category (2-digit, defaults to "04" if not specified)
            var addrCat = address.AddressCategory.HasValue
                ? address.AddressCategory.Value.ToString("D2")
                : "04";
            sb.Append(TudfFieldFormatter.FormatVariableField("08", addrCat));

            // Field 09: Residence Code (optional: 01=Owned, 02=Rented)
            if (address.ResidenceCode.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("09",
                    address.ResidenceCode.Value.ToString("D2")));

            return sb.ToString();
        }

        /// <summary>
        /// Build the Account Segment (TL).
        /// Format: TL + 04 + T001 + [fields]
        /// Tags 06, 07, 23-25, 27-33 are reserved for future use — skip them.
        /// </summary>
        public string BuildAccount(AccountSegmentModel account)
        {
            var sb = new StringBuilder();
            sb.Append("TL");
            sb.Append("04");
            sb.Append("T001");

            bool isCC = CreditCardAccountTypes.Contains(account.AccountType);

            // Tag 01: Current/New Reporting Member Code (fixed 10, submitted as variable)
            sb.Append(TudfFieldFormatter.FormatVariableField("01", account.CurrentMemberCode));

            // Tag 02: Member Short Name (optional)
            if (!string.IsNullOrEmpty(account.MemberShortName))
                sb.Append(TudfFieldFormatter.FormatVariableField("02", account.MemberShortName));

            // Tag 03: Account Number
            sb.Append(TudfFieldFormatter.FormatVariableField("03", account.AccountNumber));

            // Tag 04: Account Type (2-digit zero-padded)
            sb.Append(TudfFieldFormatter.FormatVariableField("04", account.AccountType.ToString("D2")));

            // Tag 05: Ownership Indicator (NOT zero-padded — single digit)
            sb.Append(TudfFieldFormatter.FormatVariableField("05", account.OwnershipIndicator.ToString()));

            // Tags 06, 07: RESERVED — skip

            // Tag 08: Date Opened/Disbursed (Required, ddMMyyyy)
            sb.Append(TudfFieldFormatter.FormatVariableDateField("08", account.DateOpenedDisbursed));

            // Tag 09: Date of Last Payment (Optional)
            if (account.DateOfLastPayment.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("09", account.DateOfLastPayment));

            // Tag 10: Date Closed (Optional)
            if (account.DateClosed.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("10", account.DateClosed));

            // Tag 11: Date Reported and Certified
            sb.Append(TudfFieldFormatter.FormatVariableDateField("11", account.DateReportedAndCertified));

            // Tag 12: High Credit/Sanctioned Amount (Required per v3.72)
            sb.Append(TudfFieldFormatter.FormatNumericVariableField("12", account.HighCreditSanctionedAmount));

            // Tag 13: Current Balance (Required) — signed amount
            if (isCC && account.CurrentBalance == 0)
                sb.Append(TudfFieldFormatter.FormatVariableField("13", "0"));
            else
                sb.Append(TudfFieldFormatter.FormatSignedAmountField("13",
                    account.CurrentBalance, account.IsCurrentBalanceNegative));

            // Tag 14: Amount Overdue (Optional)
            if (account.AmountOverdue.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("14", account.AmountOverdue));

            // Tag 15: Number of Days Past Due
            // Per spec: report "0" when current; 3-digit for > 0; max 900
            if (account.NumberOfDaysPastDue.HasValue)
            {
                var ndpd = account.NumberOfDaysPastDue.Value;
                if (ndpd > 900) ndpd = 900;
                var ndpdStr = ndpd == 0 ? "0" : ndpd.ToString("D3");
                sb.Append(TudfFieldFormatter.FormatVariableField("15", ndpdStr));
            }
            else if (account.AmountOverdue.HasValue && account.AmountOverdue > 0 && isCC)
            {
                // CC with overdue but no NDPD: write 000
                sb.Append(TudfFieldFormatter.FormatVariableField("15", "000"));
            }

            // Tags 16-20: Old member/account info (Optional)
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

            // Tag 21: Suit Filed / Wilful Default (Optional, 2-digit)
            if (account.SuitFiledWilfulDefault.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("21",
                    account.SuitFiledWilfulDefault.Value.ToString("D2")));

            // Tag 22: Credit Facility Status (When Applicable, 2-digit)
            // Tags 23-25: RESERVED — skip
            if (account.CreditFacilityStatus.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("22",
                    account.CreditFacilityStatus.Value.ToString("D2")));

            // Tag 26: Asset Classification (either tag 15 or 26 must be present)
            // Tags 27-33: RESERVED — skip
            if (account.AssetClassification.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("26",
                    account.AssetClassification.Value.ToString("D2")));

            // Tag 34: Value of Collateral (Optional)
            if (account.ValueOfCollateral.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("34", account.ValueOfCollateral));

            // Tag 35: Type of Collateral (Optional, 2-digit)
            if (account.TypeOfCollateral.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("35",
                    account.TypeOfCollateral.Value.ToString("D2")));

            // Tag 36: Credit Limit (only for CC account types 10, 16, 31, 35)
            if (account.CreditLimit.HasValue && isCC)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("36", account.CreditLimit));

            // Tag 37: Cash Limit (only for CC account types 10, 16, 31, 35)
            if (account.CashLimit.HasValue && isCC)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("37", account.CashLimit));

            // Tag 38: Rate Of Interest (Optional, decimal format d.ddd)
            if (!string.IsNullOrEmpty(account.RateOfInterest))
            {
                var roi = TudfFieldFormatter.FormatRateOfInterest(account.RateOfInterest);
                if (!string.IsNullOrEmpty(roi))
                    sb.Append(TudfFieldFormatter.FormatVariableField("38", roi));
            }

            // Tag 39: Repayment Tenure (Optional, 3-digit months count)
            if (account.RepaymentTenure.HasValue && account.RepaymentTenure.Value > 0)
                sb.Append(TudfFieldFormatter.FormatVariableField("39",
                    account.RepaymentTenure.Value.ToString("D3")));

            // Tag 40: EMI Amount (not for CC types 10,16,31,35 or type 12 Overdraft)
            var noEmiTypes = new[] { 10, 12, 16, 31, 35 };
            if (account.EmiAmount.HasValue && account.EmiAmount.Value > 0 &&
                !noEmiTypes.Contains(account.AccountType))
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("40", account.EmiAmount));

            // Tag 41: Written-off Amount Total (when CFS = 02, 03, 04)
            if (account.WrittenOffAmountTotal.HasValue && account.WrittenOffAmountTotal.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("41", account.WrittenOffAmountTotal));

            // Tag 42: Written-off Amount Principal (when CFS = 02, 03, 04)
            if (account.WrittenOffAmountPrincipal.HasValue && account.WrittenOffAmountPrincipal.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("42", account.WrittenOffAmountPrincipal));

            // Tag 43: Settlement Amount (when CFS = 03, 04, 15, 16)
            if (account.SettlementAmount.HasValue && account.SettlementAmount.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("43", account.SettlementAmount));

            // Tag 44: Payment Frequency (Optional, 2-digit)
            if (account.PaymentFrequency.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("44",
                    account.PaymentFrequency.Value.ToString("D2")));

            // Tag 45: Actual Payment Amount (Optional, > 0)
            if (account.ActualPaymentAmount.HasValue && account.ActualPaymentAmount.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("45", account.ActualPaymentAmount));

            // Tag 46: Occupation Code (Optional, 2-digit)
            if (account.OccupationCode.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("46",
                    account.OccupationCode.Value.ToString("D2")));

            // Tag 47: Income (Optional, > 0)
            if (account.Income.HasValue && account.Income.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("47", account.Income));

            // Tag 48: Net/Gross Income Indicator (Optional, "G" or "N")
            if (!string.IsNullOrEmpty(account.NetGrossIncomeIndicator))
                sb.Append(TudfFieldFormatter.FormatVariableField("48", account.NetGrossIncomeIndicator));

            // Tag 49: Monthly/Annual Income Indicator (Optional, "M" or "A")
            if (!string.IsNullOrEmpty(account.MonthlyAnnualIncomeIndicator))
                sb.Append(TudfFieldFormatter.FormatVariableField("49", account.MonthlyAnnualIncomeIndicator));

            return sb.ToString();
        }

        /// <summary>
        /// Build an Account History Segment (TH).
        /// Format: TH + 03 + H[nn] + [fields]
        /// TH can occur maximum 47 times per record (H01 to H47) per UCRF v3.74 spec text.
        /// Field 01 (Account History Date) is required: day is always reset to 01.
        /// Field 02 (Asset Classification / NDPD) is required: 3-char STD/SUB/DBT/LSS/SMA or 3-digit number.
        /// </summary>
        public string BuildAccountHistory(AccountHistoryModel history)
        {
            var sb = new StringBuilder();
            var segTag = "H" + history.SegmentIndex.ToString("D2");

            sb.Append("TH");
            sb.Append("03");
            sb.Append(segTag);

            // Tag 01: Account History Date (Required, ddMMyyyy, day reset to 01)
            var histDate = history.AccountHistoryDate != default
                ? new DateTime(history.AccountHistoryDate.Year, history.AccountHistoryDate.Month, 1)
                : default;
            sb.Append(TudfFieldFormatter.FormatVariableField("01",
                TudfFieldFormatter.FormatDate(histDate)));

            // Tag 02: Asset Classification / NDPD (Required, 3 chars)
            // Valid: STD, SUB, DBT, LSS, SMA, or 3-digit number (000-900)
            var ndpdVal = (history.AssetClassificationNdpd ?? "").Trim().ToUpperInvariant();
            var validTextValues = new HashSet<string> { "STD", "SUB", "DBT", "LSS", "SMA" };
            string ndpdFormatted;
            if (validTextValues.Contains(ndpdVal))
            {
                ndpdFormatted = ndpdVal; // already 3 chars
            }
            else if (int.TryParse(ndpdVal, out int ndpdNum))
            {
                if (ndpdNum > 900) ndpdNum = 900;
                ndpdFormatted = ndpdNum.ToString("D3");
            }
            else
            {
                ndpdFormatted = "000"; // default to current (0 DPD)
            }
            sb.Append(TudfFieldFormatter.FormatVariableField("02", ndpdFormatted));

            // Tag 03: Amount Overdue (Optional, > 0)
            if (history.AmountOverdue.HasValue && history.AmountOverdue.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("03", history.AmountOverdue));

            // Tag 04: High Credit/Sanctioned Amount (Optional)
            if (history.HighCreditSanctionedAmount.HasValue && history.HighCreditSanctionedAmount.Value >= 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("04", history.HighCreditSanctionedAmount));

            // Tag 05: Credit Limit (Optional — only for account type 10)
            if (history.CreditLimit.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("05", history.CreditLimit));

            // Tag 06: Cash Limit (Optional — only for account type 10)
            if (history.CashLimit.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("06", history.CashLimit));

            // Tag 07: Current Balance (Required) — signed
            sb.Append(TudfFieldFormatter.FormatSignedAmountField("07",
                history.CurrentBalance, history.IsCurrentBalanceNegative));

            // Tag 08: Date of Last Payment (Optional)
            if (history.DateOfLastPayment.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("08", history.DateOfLastPayment));

            // Tag 09: Actual Payment Amount (Optional, > 0)
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

            // One Header per file
            sb.Append(builder.BuildHeader(header));

            foreach (var record in records)
            {
                // Order per UCRF spec: PN, ID, PT, EC, PA, TL, TH, ES
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

            // One Trailer per file
            sb.Append(builder.BuildTrailerSegment());

            return sb.ToString();
        }
    }
}
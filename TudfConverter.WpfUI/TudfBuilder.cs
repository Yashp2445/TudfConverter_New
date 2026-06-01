using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace TudfConverter.WpfUI
{
    public static class TudfFieldFormatter
    {
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

        public static string FormatSignedAmountField(string tag, long amount, bool isNegative)
        {
            var amountStr = amount.ToString();
            if (isNegative) amountStr += "-";
            return FormatVariableField(tag, amountStr);
        }

        public static string FormatFixedAlpha(string? value, int length)
        {
            var v = value ?? string.Empty;
            if (v.Length > length) v = v.Substring(0, length);
            return v.PadRight(length);
        }

        public static string FormatFixedNumeric(string? value, int length)
        {
            var v = value ?? string.Empty;
            if (v.Length > length) v = v.Substring(v.Length - length);
            return v.PadLeft(length, '0');
        }

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
    }

    public class TudfSegmentBuilder
    {
        private static readonly int[] CreditCardAccountTypes = { 10, 16, 31, 35 };

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
                throw new InvalidOperationException($"HD segment length {result.Length} does not equal 146 bytes.");
            return result;
        }

        public string BuildName(NameSegmentModel name)
        {
            var sb = new StringBuilder();
            sb.Append("PN");
            sb.Append("03");
            sb.Append("N01");

            var words = (name.FullName ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var packedTags = new List<string>();
            int wordIndex = 0;

            while (wordIndex < words.Length && packedTags.Count < 5)
            {
                var word = words[wordIndex].Length > 25 ? words[wordIndex].Substring(0, 25) : words[wordIndex];
                var current = word;
                wordIndex++;

                while (wordIndex < words.Length)
                {
                    var nextWord = words[wordIndex];
                    if (current.Length + 1 + nextWord.Length <= 25)
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

            if (name.DateOfBirth.HasValue) sb.Append(TudfFieldFormatter.FormatVariableDateField("07", name.DateOfBirth));
            if (name.Gender.HasValue) sb.Append(TudfFieldFormatter.FormatVariableField("08", name.Gender.Value.ToString()));

            return sb.ToString();
        }

        public string BuildIdentification(IdentificationModel id)
        {
            var sb = new StringBuilder();
            var segTag = "I" + id.SegmentIndex.ToString("D2");
            sb.Append("ID");
            sb.Append("03");
            sb.Append(segTag);

            sb.Append(TudfFieldFormatter.FormatVariableField("01", id.IdType.ToString("D2")));
            sb.Append(TudfFieldFormatter.FormatVariableField("02", id.IdNumber));

            if (id.IssueDate.HasValue) sb.Append(TudfFieldFormatter.FormatVariableDateField("03", id.IssueDate));
            if (id.ExpirationDate.HasValue) sb.Append(TudfFieldFormatter.FormatVariableDateField("04", id.ExpirationDate));

            return sb.ToString();
        }

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

        public string BuildAddress(AddressModel address)
        {
            var sb = new StringBuilder();
            var segTag = "A" + address.SegmentIndex.ToString("D2");
            sb.Append("PA");
            sb.Append("03");
            sb.Append(segTag);

            sb.Append(TudfFieldFormatter.FormatVariableField("01", address.AddressLine1));
            if (!string.IsNullOrEmpty(address.AddressLine2))
                sb.Append(TudfFieldFormatter.FormatVariableField("02", address.AddressLine2));
            if (!string.IsNullOrEmpty(address.AddressLine3))
                sb.Append(TudfFieldFormatter.FormatVariableField("03", address.AddressLine3));
            if (!string.IsNullOrEmpty(address.AddressLine4))
                sb.Append(TudfFieldFormatter.FormatVariableField("04", address.AddressLine4));
            if (!string.IsNullOrEmpty(address.AddressLine5))
                sb.Append(TudfFieldFormatter.FormatVariableField("05", address.AddressLine5));

            if (!string.IsNullOrEmpty(address.StateCode))
                sb.Append(TudfFieldFormatter.FormatVariableField("06", TudfFieldFormatter.FormatFixedNumeric(address.StateCode, 2)));

            if (!string.IsNullOrEmpty(address.PinCode))
            {
                var digitsOnly = new string(address.PinCode.Where(char.IsDigit).ToArray());
                if (digitsOnly.Length > 0)
                {
                    var pinFormatted = TudfFieldFormatter.FormatFixedNumeric(digitsOnly, 6);
                    sb.Append(TudfFieldFormatter.FormatVariableField("07", pinFormatted));
                }
            }

            var addrCat = address.AddressCategory.HasValue ? address.AddressCategory.Value.ToString("D2") : "04";
            sb.Append(TudfFieldFormatter.FormatVariableField("08", addrCat));

            if (address.ResidenceCode.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("09", address.ResidenceCode.Value.ToString("D2")));

            return sb.ToString();
        }

        public string BuildAccount(AccountSegmentModel account)
        {
            var sb = new StringBuilder();
            sb.Append("TL");
            sb.Append("04");
            sb.Append("T001");

            // Tag 01: Current/New Reporting Member Code (fixed 10, but write as variable)
            sb.Append(TudfFieldFormatter.FormatVariableField("01", account.CurrentMemberCode));

            // Tag 02: Member Short Name (optional)
            if (!string.IsNullOrEmpty(account.MemberShortName))
                sb.Append(TudfFieldFormatter.FormatVariableField("02", account.MemberShortName));

            // Tag 03: Account Number
            sb.Append(TudfFieldFormatter.FormatVariableField("03", account.AccountNumber));

            // Tag 04: Account Type (2-digit zero-padded)
            sb.Append(TudfFieldFormatter.FormatVariableField("04", account.AccountType.ToString("D2")));

            // Tag 05: Ownership Indicator — NOT zero-padded, just the digit value
            sb.Append(TudfFieldFormatter.FormatVariableField("05", account.OwnershipIndicator.ToString()));

            // Tags 06, 07 are RESERVED FOR FUTURE USE per spec — skip

            // Tag 08: Date Opened/Disbursed (Required)
            sb.Append(TudfFieldFormatter.FormatVariableDateField("08", account.DateOpenedDisbursed));

            // Tag 09: Date of Last Payment (Optional)
            if (account.DateOfLastPayment.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("09", account.DateOfLastPayment));

            // Tag 10: Date Closed (Optional)
            if (account.DateClosed.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("10", account.DateClosed));

            // Tag 11: Date Reported and Certified
            sb.Append(TudfFieldFormatter.FormatVariableDateField("11", account.DateReportedAndCertified));

            // Tag 12: High Credit/Sanctioned Amount (Required)
            sb.Append(TudfFieldFormatter.FormatNumericVariableField("12", account.HighCreditSanctionedAmount));

            // Tag 13: Current Balance (Required) — signed
            bool isCC = CreditCardAccountTypes.Contains(account.AccountType);
            if (isCC && account.CurrentBalance == 0)
                sb.Append(TudfFieldFormatter.FormatVariableField("13", "0"));
            else
                sb.Append(TudfFieldFormatter.FormatSignedAmountField("13", account.CurrentBalance, account.IsCurrentBalanceNegative));

            // Tag 14: Amount Overdue — write even when 0 (per expected output pattern)
            // When overdue is null (not provided), skip. When 0 or positive, always write.
            if (account.AmountOverdue.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("14", account.AmountOverdue));

            // Tag 15: Number of Days Past Due
            // Write as actual value string (not zero-padded to 3 digits) when value is 0
            // For positive values cap at 900
            if (account.NumberOfDaysPastDue.HasValue)
            {
                var ndpd = account.NumberOfDaysPastDue.Value;
                if (ndpd > 900) ndpd = 900;
                // Write as plain number string (no leading zeros for zero, 3-digit for >0)
                var ndpdStr = ndpd == 0 ? "0" : ndpd.ToString("D3");
                sb.Append(TudfFieldFormatter.FormatVariableField("15", ndpdStr));
            }
            else if (account.AmountOverdue.HasValue && account.AmountOverdue > 0 && isCC)
            {
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
                sb.Append(TudfFieldFormatter.FormatVariableField("19", account.OldAccountType.Value.ToString("D2")));
            if (account.OldOwnershipIndicator.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("20", account.OldOwnershipIndicator.Value.ToString("D2")));

            // Tag 21: Suit Filed / Wilful Default (Optional)
            if (account.SuitFiledWilfulDefault.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("21", account.SuitFiledWilfulDefault.Value.ToString("D2")));

            // Tag 22: Credit Facility Status
            // Tags 23-25 are RESERVED FOR FUTURE USE — skip
            if (account.CreditFacilityStatus.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("22", account.CreditFacilityStatus.Value.ToString("D2")));

            // Tag 26: Asset Classification (either tag 15 or 26 must be present)
            // Tags 27-33 reserved — skip
            if (account.AssetClassification.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("26", account.AssetClassification.Value.ToString("D2")));

            // Tag 34: Value of Collateral (Optional)
            if (account.ValueOfCollateral.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("34", account.ValueOfCollateral));

            // Tag 35: Type of Collateral (Optional)
            if (account.TypeOfCollateral.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("35", account.TypeOfCollateral.Value.ToString("D2")));

            // Tag 36: Credit Limit (only for CC types)
            if (account.CreditLimit.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("36", account.CreditLimit));

            // Tag 37: Cash Limit (only for CC types)
            if (account.CashLimit.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("37", account.CashLimit));

            // Tag 38: Rate Of Interest (Optional)
            if (!string.IsNullOrEmpty(account.RateOfInterest))
                sb.Append(TudfFieldFormatter.FormatVariableField("38", account.RateOfInterest));

            // Tag 39: Repayment Tenure (Optional)
            if (account.RepaymentTenure.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("39", account.RepaymentTenure.Value.ToString("D3")));

            // Tag 40: EMI Amount (Optional)
            if (account.EmiAmount.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("40", account.EmiAmount));

            // Tag 41: Written-off Amount Total
            if (account.WrittenOffAmountTotal.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("41", account.WrittenOffAmountTotal));

            // Tag 42: Written-off Amount Principal
            if (account.WrittenOffAmountPrincipal.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("42", account.WrittenOffAmountPrincipal));

            // Tag 43: Settlement Amount
            if (account.SettlementAmount.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("43", account.SettlementAmount));

            // Tag 44: Payment Frequency (Optional)
            if (account.PaymentFrequency.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("44", account.PaymentFrequency.Value.ToString("D2")));

            // Tag 45: Actual Payment Amount (Optional)
            if (account.ActualPaymentAmount.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("45", account.ActualPaymentAmount));

            // Tag 46: Occupation Code (Optional)
            if (account.OccupationCode.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableField("46", account.OccupationCode.Value.ToString("D2")));

            // Tag 47: Income (Optional)
            if (account.Income.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("47", account.Income));

            // Tag 48: Net/Gross Income Indicator (Optional)
            if (!string.IsNullOrEmpty(account.NetGrossIncomeIndicator))
                sb.Append(TudfFieldFormatter.FormatVariableField("48", account.NetGrossIncomeIndicator));

            // Tag 49: Monthly/Annual Income Indicator (Optional)
            if (!string.IsNullOrEmpty(account.MonthlyAnnualIncomeIndicator))
                sb.Append(TudfFieldFormatter.FormatVariableField("49", account.MonthlyAnnualIncomeIndicator));

            return sb.ToString();
        }

        public string BuildAccountHistory(AccountHistoryModel history)
        {
            var sb = new StringBuilder();
            var segTag = "H" + history.SegmentIndex.ToString("D2");

            sb.Append("TH");
            sb.Append("03");
            sb.Append(segTag);

            // Tag 01: Account History Date (Required, 8 digits)
            sb.Append(TudfFieldFormatter.FormatVariableField("01", TudfFieldFormatter.FormatDate(history.AccountHistoryDate)));

            // Tag 02: Asset Classification / NDPD (Required, 3 chars)
            sb.Append(TudfFieldFormatter.FormatVariableField("02", TudfFieldFormatter.FormatFixedAlpha(history.AssetClassificationNdpd, 3)));

            // Tag 03: Amount Overdue (Optional)
            if (history.AmountOverdue.HasValue && history.AmountOverdue.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("03", history.AmountOverdue));

            // Tag 04: High Credit/Sanctioned Amount (Optional)
            if (history.HighCreditSanctionedAmount.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("04", history.HighCreditSanctionedAmount));

            // Tag 05: Credit Limit (Optional — only for account type 10)
            if (history.CreditLimit.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("05", history.CreditLimit));

            // Tag 06: Cash Limit (Optional — only for account type 10)
            if (history.CashLimit.HasValue)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("06", history.CashLimit));

            // Tag 07: Current Balance (Required) — signed
            sb.Append(TudfFieldFormatter.FormatSignedAmountField("07", history.CurrentBalance, history.IsCurrentBalanceNegative));

            // Tag 08: Date of Last Payment (Optional)
            if (history.DateOfLastPayment.HasValue)
                sb.Append(TudfFieldFormatter.FormatVariableDateField("08", history.DateOfLastPayment));

            // Tag 09: Actual Payment Amount (Optional)
            if (history.ActualPaymentAmount.HasValue && history.ActualPaymentAmount.Value > 0)
                sb.Append(TudfFieldFormatter.FormatNumericVariableField("09", history.ActualPaymentAmount));

            return sb.ToString();
        }

        public string BuildEndSegment()
        {
            return "ES02**";
        }

        public string BuildTrailerSegment()
        {
            return "TRLR";
        }
    }

    public class TudfGeneratorService
    {
        public string Generate(List<CustomerRecord> records, HeaderSegmentModel header)
        {
            var sb = new StringBuilder();
            var builder = new TudfSegmentBuilder();

            // Write header (one per file)
            sb.Append(builder.BuildHeader(header));

            foreach (var record in records)
            {
                sb.Append(builder.BuildName(record.Name));
                foreach (var id in record.Identifications) sb.Append(builder.BuildIdentification(id));
                foreach (var phone in record.Telephones) sb.Append(builder.BuildTelephone(phone));
                foreach (var email in record.Emails) sb.Append(builder.BuildEmail(email));
                foreach (var address in record.Addresses) sb.Append(builder.BuildAddress(address));
                sb.Append(builder.BuildAccount(record.Account));
                foreach (var history in record.AccountHistory) sb.Append(builder.BuildAccountHistory(history));
                sb.Append(builder.BuildEndSegment());
            }

            // Write trailer (one per file, mandatory)
            sb.Append(builder.BuildTrailerSegment());

            return sb.ToString();
        }
    }
}
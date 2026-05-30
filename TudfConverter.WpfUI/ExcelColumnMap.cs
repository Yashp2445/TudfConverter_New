using System.Collections.Generic;

namespace TudfConverter.WpfUI
{
    public static class ExcelColumnMap
    {
        public const string ConsumerName = "Consumer Name";
        public const string DateOfBirth = "Date of Birth";
        public const string Gender = "Gender";

        public const string IncomeTaxIdNumber = "Income Tax ID Number";
        public const string PassportNumber = "Passport Number";
        public const string PassportIssueDate = "Passport Issue Date";
        public const string PassportExpiryDate = "Passport Expiry Date";
        public const string VoterIdNumber = "Voter ID Number";
        public const string DrivingLicenseNumber = "Driving License Number";
        public const string DrivingLicenseIssueDate = "Driving License Issue Date";
        public const string DrivingLicenseExpiryDate = "Driving License Expiry Date";
        public const string RationCardNumber = "Ration Card Number";
        public const string UniversalIdNumber = "Universal ID Number";
        public const string AdditionalId1 = "Additional ID #1";
        public const string AdditionalId2 = "Additional ID #2";
        public const string Ckyc = "CKYC";
        public const string NregaCardNumber = "NREGA Card Number";

        public const string TelephoneNoMobile = "Telephone No.Mobile";
        public const string TelephoneNoResidence = "Telephone No.Residence";
        public const string TelephoneNoOffice = "Telephone No.Office";
        public const string ExtensionOffice = "Extension Office";
        public const string TelephoneNoOther = "Telephone No.Other";
        public const string ExtensionOther = "Extension Other";

        public const string EmailId1 = "Email ID 1";
        public const string EmailId2 = "Email ID 2";

        public const string AddressLine1 = "Address Line 1";
        public const string StateCode1 = "State Code 1";
        public const string PinCode1 = "PIN Code 1";
        public const string AddressCategory1 = "Address Category 1";
        public const string ResidenceCode1 = "Residence Code 1";
        public const string AddressLine2 = "Address Line 2";
        public const string StateCode2 = "State Code 2";
        public const string PinCode2 = "PIN Code 2";
        public const string AddressCategory2 = "Address Category 2";
        public const string ResidenceCode2 = "Residence Code 2";

        public const string CurrentNewMemberCode = "Current/New Member Code";
        public const string CurrentNewMemberShortName = "Current/New Member Short Name";
        public const string CurrNewAccountNo = "Curr/New Account No";
        public const string AccountType = "Account Type";
        public const string OwnershipIndicator = "Ownership Indicator";
        public const string DateOpenedDisbursed = "Date Opened/Disbursed";
        public const string DateOfLastPayment = "Date of Last Payment";
        public const string DateClosed = "Date Closed";
        public const string DateReportedAndCertified = "Date Reported";
        public const string HighCreditSanctionedAmt = "High Credit/Sanctioned Amt";
        public const string CurrentBalance = "Current  Balance";
        public const string AmtOverdue = "Amt Overdue";
        public const string NoOfDaysPastDue = "No of Days Past Due";
        public const string OldMbrCode = "Old Mbr Code";
        public const string OldMbrShortName = "Old Mbr Short Name";
        public const string OldAccNo = "Old Acc No";
        public const string OldAccType = "Old Acc Type";
        public const string OldOwnershipIndicator = "Old Ownership Indicator";
        public const string SuitFiledWilfulDefault = "Suit Filed / Wilful Default";
        public const string AssetClassification = "Asset Classification";
        public const string ValueOfCollateral = "Value of Collateral";
        public const string TypeOfCollateral = "Type of Collateral";
        public const string CreditLimit = "Credit Limit";
        public const string CashLimit = "Cash Limit";
        public const string RateOfInterest = "Rate of Interest";
        public const string RepaymentTenure = "RepaymentTenure";
        public const string EmiAmount = "EMI Amount";
        public const string WrittenOffAmountTotal = "Written- off Amount (Total)";
        public const string WrittenOffPrincipalAmount = "Written- off Principal Amount";
        public const string SettlementAmt = "Settlement Amt";
        public const string PaymentFrequency = "Payment Frequency";
        public const string ActualPaymentAmt = "Actual Payment Amt";
        public const string OccupationCode = "Occupation Code";
        public const string Income = "Income";
        public const string NetGrossIncomeIndicator = "Net/Gross Income Indicator";
        public const string MonthlyAnnualIncomeIndicator = "Monthly/Annual Income Indicator";
        public const string CreditFacilityStatus = "Credit Facility Status";

        public static HashSet<string> GetAllExpectedColumns()
        {
            return new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ConsumerName, DateOfBirth, Gender,
                IncomeTaxIdNumber, PassportNumber, PassportIssueDate, PassportExpiryDate,
                VoterIdNumber, DrivingLicenseNumber, DrivingLicenseIssueDate, DrivingLicenseExpiryDate,
                RationCardNumber, UniversalIdNumber, AdditionalId1, AdditionalId2, Ckyc, NregaCardNumber,
                TelephoneNoMobile, TelephoneNoResidence, TelephoneNoOffice, ExtensionOffice,
                TelephoneNoOther, ExtensionOther,
                EmailId1, EmailId2,
                AddressLine1, StateCode1, PinCode1, AddressCategory1, ResidenceCode1,
                AddressLine2, StateCode2, PinCode2, AddressCategory2, ResidenceCode2,
                CurrentNewMemberCode, CurrentNewMemberShortName, CurrNewAccountNo, AccountType,
                OwnershipIndicator, DateOpenedDisbursed, DateOfLastPayment, DateClosed,
                DateReportedAndCertified, HighCreditSanctionedAmt, CurrentBalance, AmtOverdue, NoOfDaysPastDue,
                OldMbrCode, OldMbrShortName, OldAccNo, OldAccType, OldOwnershipIndicator,
                SuitFiledWilfulDefault, AssetClassification, ValueOfCollateral, TypeOfCollateral,
                CreditLimit, CashLimit, RateOfInterest, RepaymentTenure, EmiAmount,
                WrittenOffAmountTotal, WrittenOffPrincipalAmount, SettlementAmt,
                PaymentFrequency, ActualPaymentAmt, OccupationCode, Income,
                NetGrossIncomeIndicator, MonthlyAnnualIncomeIndicator, CreditFacilityStatus
            };
        }
    }
}

using System;
using System.Collections.Generic;

namespace TudfConverter.WpfUI
{
    public class CustomerRecord
    {
        public int RowNumber { get; set; }
        public NameSegmentModel Name { get; set; } = new NameSegmentModel();
        public List<IdentificationModel> Identifications { get; set; } = new List<IdentificationModel>();
        public List<TelephoneModel> Telephones { get; set; } = new List<TelephoneModel>();
        public List<EmailModel> Emails { get; set; } = new List<EmailModel>();
        public List<AddressModel> Addresses { get; set; } = new List<AddressModel>();
        public AccountSegmentModel Account { get; set; } = new AccountSegmentModel();
        public List<AccountHistoryModel> AccountHistory { get; set; } = new List<AccountHistoryModel>();
    }

    public class NameSegmentModel
    {
        public string? FullName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? Gender { get; set; }
    }

    public class IdentificationModel
    {
        public int SegmentIndex { get; set; }
        public int IdType { get; set; }
        public string IdNumber { get; set; } = "";
        public DateTime? IssueDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
    }

    public class TelephoneModel
    {
        public int SegmentIndex { get; set; }
        public string TelephoneNumber { get; set; } = "";
        public string? TelephoneExtension { get; set; }
        public string TelephoneType { get; set; } = "";
    }

    public class EmailModel
    {
        public int SegmentIndex { get; set; }
        public string EmailId { get; set; } = "";
    }

    public class AddressModel
    {
        public int SegmentIndex { get; set; }
        public string AddressLine1 { get; set; } = "";
        public string? AddressLine2 { get; set; }
        public string? AddressLine3 { get; set; }
        public string? AddressLine4 { get; set; }
        public string? AddressLine5 { get; set; }
        public string? StateCode { get; set; }
        public string? PinCode { get; set; }
        public int? AddressCategory { get; set; }
        public int? ResidenceCode { get; set; }
    }

    public class AccountSegmentModel
    {
        public string CurrentMemberCode { get; set; } = "";
        public string? MemberShortName { get; set; }
        public string AccountNumber { get; set; } = "";
        public int AccountType { get; set; }
        public int OwnershipIndicator { get; set; }
        public DateTime DateOpenedDisbursed { get; set; }
        public DateTime? DateOfLastPayment { get; set; }
        public DateTime? DateClosed { get; set; }
        public DateTime DateReportedAndCertified { get; set; }
        public long HighCreditSanctionedAmount { get; set; }
        public long CurrentBalance { get; set; }
        public bool IsCurrentBalanceNegative { get; set; }
        public long? AmountOverdue { get; set; }
        public int? NumberOfDaysPastDue { get; set; }
        public int? AssetClassification { get; set; }
        public int? SuitFiledWilfulDefault { get; set; }
        public int? CreditFacilityStatus { get; set; }
        public string? OldReportingMemberCode { get; set; }
        public string? OldMemberShortName { get; set; }
        public string? OldAccountNumber { get; set; }
        public int? OldAccountType { get; set; }
        public int? OldOwnershipIndicator { get; set; }
        public long? CreditLimit { get; set; }
        public long? CashLimit { get; set; }
        public string? RateOfInterest { get; set; }
        public int? RepaymentTenure { get; set; }
        public long? EmiAmount { get; set; }
        public long? WrittenOffAmountTotal { get; set; }
        public long? WrittenOffAmountPrincipal { get; set; }
        public long? SettlementAmount { get; set; }
        public int? PaymentFrequency { get; set; }
        public long? ActualPaymentAmount { get; set; }
        public int? OccupationCode { get; set; }
        public long? Income { get; set; }
        public string? NetGrossIncomeIndicator { get; set; }
        public string? MonthlyAnnualIncomeIndicator { get; set; }
        public long? ValueOfCollateral { get; set; }
        public int? TypeOfCollateral { get; set; }
    }

    public class HeaderSegmentModel
    {
        public string SegmentTag { get; set; } = "TUDF";
        public string Version { get; set; } = "12";
        public string MemberUserId { get; set; } = "";
        public string ShortName { get; set; } = "";      
        public string ReportingCycle { get; set; } = "";
        public DateTime DateReportedAndCertified { get; set; }  
        public string FutureUse1 { get; set; } = "";
        public string FutureUse2 { get; set; } = "A";
        public string FutureUse3 { get; set; } = "00000";
        public string MemberData { get; set; } = "";
    }

    public class AccountHistoryModel
    {
        public int SegmentIndex { get; set; }
        public DateTime AccountHistoryDate { get; set; }
        public string AssetClassificationNdpd { get; set; } = "";
        public long? AmountOverdue { get; set; }
        public long? HighCreditSanctionedAmount { get; set; }
        public long? CreditLimit { get; set; }
        public long? CashLimit { get; set; }
        public long CurrentBalance { get; set; }
        public bool IsCurrentBalanceNegative { get; set; }
        public DateTime? DateOfLastPayment { get; set; }
        public long? ActualPaymentAmount { get; set; }
    }

    public class FileProcessingResult
    {
        public bool IsSuccess { get; set; }
        public int TotalRows { get; set; }
        public int AcceptedRows { get; set; }
        public int RejectedRows { get; set; }
        public string? GeneratedFilePath { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

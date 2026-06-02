using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TudfConverter.WpfUI
{
    // ────────────────────────────────────────────────────────────────
    //  Orchestrator
    // ────────────────────────────────────────────────────────────────
    public class TudfValidator
    {
        private readonly DateTime _headerDateReported;

        public TudfValidator(DateTime headerDateReported)
        {
            _headerDateReported = headerDateReported;
        }

        public RecordValidationResult Validate(CustomerRecord record)
        {
            var result = new RecordValidationResult { RowNumber = record.RowNumber };

            NameSegmentValidator.Validate(record, result);
            IdentificationValidator.Validate(record, result);
            TelephoneValidator.Validate(record, result);
            EmailValidator.Validate(record, result);
            AddressValidator.Validate(record, result);
            AccountValidator.Validate(record, result);
            CrossSegmentValidator.Validate(record, result, _headerDateReported);

            result.IsRecordRejected = result.Errors.Any(e => e.Outcome == FailureOutcome.RejectRecord);
            return result;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Helper
    // ────────────────────────────────────────────────────────────────
    internal static class ValidationHelper
    {
        public static void Add(RecordValidationResult result, int row, string segment,
            string code, string field, string message, FailureOutcome outcome)
        {
            result.Errors.Add(new ValidationError
            {
                RowNumber = row,
                SegmentTag = segment,
                ErrorCode = code,
                FieldName = field,
                ErrorMessage = message,
                Outcome = outcome
            });
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  PN — Name Segment
    // ────────────────────────────────────────────────────────────────
    internal static class NameSegmentValidator
    {
        // Disallowed characters in name
        private static readonly HashSet<char> DisallowedChars = new HashSet<char>
        {
            '~', '!', '#', '$', '%', '^', '&', '*', '=', '|', '?', '+', ',', '@'
        };

        // Allowed slash patterns (salutations)
        private static readonly HashSet<string> AllowedSlashPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "S/O", "W/O", "H/O", "D/O"
        };

        public static void Validate(CustomerRecord record, RecordValidationResult result)
        {
            var name = record.Name;
            int row = record.RowNumber;

            // PN-01: Consumer name must not be empty
            if (string.IsNullOrWhiteSpace(name?.FullName))
            {
                ValidationHelper.Add(result, row, "PN", "PN-01", "Consumer Name",
                    "Consumer name is required.", FailureOutcome.RejectRecord);
                return; // no further PN checks possible
            }

            var fullName = name.FullName!.Trim();

            // PN-NUM: Name must not contain digits
            if (fullName.Any(char.IsDigit))
            {
                ValidationHelper.Add(result, row, "PN", "PN-NUM", "Consumer Name",
                    "Name must not contain any digits.", FailureOutcome.RejectRecord);
            }

            // PN-CHAR: Disallowed characters check
            if (ContainsDisallowedChars(fullName))
            {
                ValidationHelper.Add(result, row, "PN", "PN-CHAR", "Consumer Name",
                    "Name contains disallowed special characters.", FailureOutcome.RejectRecord);
            }

            // PN-TOKEN: At least one word with 2+ alphabetic characters
            var words = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool hasValidToken = words.Any(w => w.Count(char.IsLetter) >= 2);
            if (!hasValidToken)
            {
                ValidationHelper.Add(result, row, "PN", "PN-TOKEN", "Consumer Name",
                    "Name must contain at least one word with 2 or more alphabetic characters.",
                    FailureOutcome.RejectRecord);
            }

            // PN-08: Gender must be 1, 2, or 3 if provided
            if (name.Gender.HasValue && name.Gender.Value != 1 && name.Gender.Value != 2 && name.Gender.Value != 3)
            {
                ValidationHelper.Add(result, row, "PN", "PN-08", "Gender",
                    "Gender must be 1 (Female), 2 (Male), or 3 (Transgender).",
                    FailureOutcome.RejectField);
            }

            // PN-07: Date of Birth validation
            if (name.DateOfBirth.HasValue)
            {
                var dob = name.DateOfBirth.Value;
                if (dob < new DateTime(1900, 1, 1) || dob > DateTime.Today)
                {
                    // Determine if DOB is required (accounts opened on/after 01/05/2005)
                    bool isRequired = record.Account.DateOpenedDisbursed >= new DateTime(2005, 5, 1);
                    ValidationHelper.Add(result, row, "PN", "PN-07", "Date of Birth",
                        "Date of Birth is not a valid calendar date (must be after 01/01/1900).",
                        isRequired ? FailureOutcome.RejectRecord : FailureOutcome.RejectField);
                }
            }
        }

        private static bool ContainsDisallowedChars(string name)
        {
            // Check each character; slashes need special handling
            for (int i = 0; i < name.Length; i++)
            {
                if (DisallowedChars.Contains(name[i]))
                    return true;

                if (name[i] == '/' || name[i] == '\\')
                {
                    // Check if slash is part of an allowed salutation pattern
                    if (!IsPartOfAllowedSlashPattern(name, i))
                        return true;
                }
            }
            return false;
        }

        private static bool IsPartOfAllowedSlashPattern(string name, int slashIndex)
        {
            // Try to extract a 3-char pattern around the slash: X/O
            if (slashIndex >= 1 && slashIndex + 1 < name.Length)
            {
                string pattern = name.Substring(slashIndex - 1, 3);
                if (AllowedSlashPatterns.Contains(pattern))
                    return true;
            }
            return false;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  ID — Identification Segment
    // ────────────────────────────────────────────────────────────────
    internal static class IdentificationValidator
    {
        private static readonly HashSet<int> ValidIdTypes = new HashSet<int> { 1, 2, 3, 4, 5, 6, 9, 10 };
        private static readonly HashSet<int> RejectIdTypes = new HashSet<int> { 7, 8 };

        // PAN: 5 letters + 4 digits + 1 letter, 4th char must be P or H
        private static readonly Regex PanRegex = new Regex(@"^[A-Z]{3}[PH][A-Z][0-9]{4}[A-Z]$",
            RegexOptions.Compiled);

        // Passport: 1 or 2 letters followed by digits, total 7-10 chars
        private static readonly Regex PassportRegex = new Regex(@"^[A-Z]{1,2}[0-9]{5,8}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void Validate(CustomerRecord record, RecordValidationResult result)
        {
            int row = record.RowNumber;

            foreach (var id in record.Identifications)
            {
                // ID-01: ID Type must be valid
                if (RejectIdTypes.Contains(id.IdType))
                {
                    ValidationHelper.Add(result, row, "ID", "ID-01", "ID Type",
                        $"ID Type {id.IdType:D2} (Additional ID) is reserved for future use and must not be submitted.",
                        FailureOutcome.RejectSegment);
                    continue;
                }

                if (!ValidIdTypes.Contains(id.IdType))
                {
                    ValidationHelper.Add(result, row, "ID", "ID-01", "ID Type",
                        $"ID Type {id.IdType:D2} is not a valid identification type.",
                        FailureOutcome.RejectSegment);
                    continue;
                }

                // ID-02: ID Number must not be empty
                if (string.IsNullOrWhiteSpace(id.IdNumber))
                {
                    ValidationHelper.Add(result, row, "ID", "ID-02", "ID Number",
                        "ID Number is required when ID Type is provided.",
                        FailureOutcome.RejectSegment);
                    continue;
                }

                // ID-02-FMT: Format validation per type
                ValidateIdFormat(record.RowNumber, id, result);

                // ID-03: Issue date must be earlier than expiry date
                if (id.IssueDate.HasValue && id.ExpirationDate.HasValue)
                {
                    if (id.IssueDate.Value >= id.ExpirationDate.Value)
                    {
                        ValidationHelper.Add(result, row, "ID", "ID-03", "Issue/Expiry Date",
                            "Issue date must be earlier than expiry date.",
                            FailureOutcome.RejectField);
                    }
                }
            }
        }

        private static void ValidateIdFormat(int row, IdentificationModel id, RecordValidationResult result)
        {
            var idNum = id.IdNumber.Trim().ToUpperInvariant();

            switch (id.IdType)
            {
                case 1: // PAN
                    if (idNum.Length != 10 || !PanRegex.IsMatch(idNum))
                    {
                        ValidationHelper.Add(result, row, "ID", "ID-02-FMT", "PAN",
                            "PAN must be exactly 10 chars: 5 letters, 4 digits, 1 letter (4th char must be P or H).",
                            FailureOutcome.RejectSegment);
                    }
                    break;

                case 6: // Aadhaar
                    var aadhaarDigits = new string(idNum.Where(char.IsDigit).ToArray());
                    if (aadhaarDigits.Length != 12)
                    {
                        ValidationHelper.Add(result, row, "ID", "ID-02-FMT", "Aadhaar",
                            "Aadhaar must have exactly 12 digits.",
                            FailureOutcome.RejectSegment);
                    }
                    else if (!VerhoeffChecksum.Validate(aadhaarDigits))
                    {
                        ValidationHelper.Add(result, row, "ID", "ID-02-FMT", "Aadhaar",
                            "Aadhaar number failed Verhoeff checksum validation.",
                            FailureOutcome.RejectSegment);
                    }
                    break;

                case 2: // Passport
                    var passportClean = new string(idNum.Where(char.IsLetterOrDigit).ToArray());
                    if (passportClean.Length < 7 || passportClean.Length > 10 || !PassportRegex.IsMatch(passportClean))
                    {
                        ValidationHelper.Add(result, row, "ID", "ID-02-FMT", "Passport",
                            "Passport must be 7-10 chars: 1-2 letters followed by digits.",
                            FailureOutcome.RejectSegment);
                    }
                    break;

                // All other types: no format validation beyond non-empty (already checked)
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Verhoeff Checksum (for Aadhaar)
    // ────────────────────────────────────────────────────────────────
    internal static class VerhoeffChecksum
    {
        private static readonly int[,] D = new int[,]
        {
            {0,1,2,3,4,5,6,7,8,9},
            {1,2,3,4,0,6,7,8,9,5},
            {2,3,4,0,1,7,8,9,5,6},
            {3,4,0,1,2,8,9,5,6,7},
            {4,0,1,2,3,9,5,6,7,8},
            {5,9,8,7,6,0,4,3,2,1},
            {6,5,9,8,7,1,0,4,3,2},
            {7,6,5,9,8,2,1,0,4,3},
            {8,7,6,5,9,3,2,1,0,4},
            {9,8,7,6,5,4,3,2,1,0}
        };

        private static readonly int[,] P = new int[,]
        {
            {0,1,2,3,4,5,6,7,8,9},
            {1,5,7,6,2,8,3,0,9,4},
            {5,8,0,3,7,9,6,1,4,2},
            {8,9,1,6,0,4,3,5,2,7},
            {9,4,5,3,1,2,6,8,7,0},
            {4,2,8,6,5,7,3,9,0,1},
            {2,7,9,3,8,0,6,4,1,5},
            {7,0,4,6,9,1,3,2,5,8}
        };

        private static readonly int[] Inv = { 0, 4, 3, 2, 1, 5, 6, 7, 8, 9 };

        public static bool Validate(string number)
        {
            int c = 0;
            var digits = number.ToCharArray();
            Array.Reverse(digits);
            for (int i = 0; i < digits.Length; i++)
            {
                int digit = digits[i] - '0';
                c = D[c, P[i % 8, digit]];
            }
            return c == 0;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  PT — Telephone Segment
    // ────────────────────────────────────────────────────────────────
    internal static class TelephoneValidator
    {
        private static readonly HashSet<string> ValidTypes = new HashSet<string> { "00", "01", "02", "03" };

        public static void Validate(CustomerRecord record, RecordValidationResult result)
        {
            int row = record.RowNumber;

            foreach (var phone in record.Telephones)
            {
                // PT-01: Telephone number is required
                if (string.IsNullOrWhiteSpace(phone.TelephoneNumber))
                {
                    ValidationHelper.Add(result, row, "PT", "PT-01", "Telephone Number",
                        "Telephone number is required.",
                        FailureOutcome.RejectSegment);
                    continue;
                }

                var digits = new string(phone.TelephoneNumber.Where(char.IsDigit).ToArray());

                // PT-01-LEN: Minimum 5 digits
                if (digits.Length < 5)
                {
                    ValidationHelper.Add(result, row, "PT", "PT-01-LEN", "Telephone Number",
                        "Telephone number must have at least 5 digits.",
                        FailureOutcome.RejectSegment);
                    continue;
                }

                // PT-01-START: Must not start with digit 1
                if (digits[0] == '1')
                {
                    ValidationHelper.Add(result, row, "PT", "PT-01-START", "Telephone Number",
                        "Telephone number must not start with digit 1.",
                        FailureOutcome.RejectSegment);
                }

                // PT-01-MOBILE: For mobile type, validate Indian mobile format
                if (phone.TelephoneType == "01")
                {
                    var mobileDigits = digits;
                    // Strip ISD prefix 91 or leading 0
                    if (mobileDigits.StartsWith("91") && mobileDigits.Length > 10)
                        mobileDigits = mobileDigits.Substring(2);
                    else if (mobileDigits.StartsWith("0") && mobileDigits.Length > 10)
                        mobileDigits = mobileDigits.Substring(1);

                    if (mobileDigits.Length < 10)
                    {
                        ValidationHelper.Add(result, row, "PT", "PT-01-MOBILE", "Mobile Number",
                            "Mobile number must have at least 10 digits after stripping ISD prefix.",
                            FailureOutcome.RejectSegment);
                    }
                    else
                    {
                        char firstDigit = mobileDigits[0];
                        if (firstDigit != '5' && firstDigit != '6' && firstDigit != '7' &&
                            firstDigit != '8' && firstDigit != '9')
                        {
                            ValidationHelper.Add(result, row, "PT", "PT-01-MOBILE", "Mobile Number",
                                "Mobile number must start with 5, 6, 7, 8, or 9 after stripping ISD prefix.",
                                FailureOutcome.RejectSegment);
                        }
                    }
                }

                // PT-03: Telephone type must be valid
                if (!string.IsNullOrEmpty(phone.TelephoneType) && !ValidTypes.Contains(phone.TelephoneType))
                {
                    ValidationHelper.Add(result, row, "PT", "PT-03", "Telephone Type",
                        $"Telephone type '{phone.TelephoneType}' is not valid (must be 00, 01, 02, or 03).",
                        FailureOutcome.RejectField);
                }
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  EC — Email Contact Segment
    // ────────────────────────────────────────────────────────────────
    internal static class EmailValidator
    {
        public static void Validate(CustomerRecord record, RecordValidationResult result)
        {
            int row = record.RowNumber;

            foreach (var email in record.Emails)
            {
                if (string.IsNullOrWhiteSpace(email.EmailId)) continue;

                if (!IsValidEmail(email.EmailId.Trim()))
                {
                    ValidationHelper.Add(result, row, "EC", "EC-01", "Email ID",
                        $"Email '{email.EmailId}' is not in a valid format.",
                        FailureOutcome.RejectSegment);
                }
            }
        }

        private static bool IsValidEmail(string email)
        {
            // No spaces allowed
            if (email.Contains(" ")) return false;

            // Exactly one @ symbol
            var parts = email.Split('@');
            if (parts.Length != 2) return false;

            var local = parts[0];
            var domain = parts[1];

            if (string.IsNullOrEmpty(local)) return false;
            if (string.IsNullOrEmpty(domain)) return false;

            // Domain must have a dot
            int lastDot = domain.LastIndexOf('.');
            if (lastDot < 0) return false;

            var domainName = domain.Substring(0, lastDot);
            var tld = domain.Substring(lastDot + 1);

            // Domain name before dot must have at least 2 chars
            if (domainName.Length < 2) return false;

            // TLD must have at least 2 alphabetic characters
            if (tld.Length < 2) return false;
            if (!tld.All(char.IsLetter)) return false;

            return true;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  PA — Address Segment
    // ────────────────────────────────────────────────────────────────
    internal static class AddressValidator
    {
        private static readonly HashSet<string> ValidStateCodes = new HashSet<string>
        {
            "01","02","03","04","05","06","07","08","09","10",
            "11","12","13","14","15","16","17","18","19","20",
            "21","22","23","24","25","27","28","29","30","31",
            "32","33","34","35","36","37","99"
        };

        // State code → (minPrefix, maxPrefix) for PIN validation
        private static readonly Dictionary<string, (int min, int max)> StatePinPrefixMap =
            new Dictionary<string, (int, int)>
        {
            {"01", (18,19)}, {"02", (17,17)}, {"03", (14,16)}, {"04", (14,16)}, {"05", (24,26)},
            {"06", (12,13)}, {"07", (11,11)}, {"08", (30,34)}, {"09", (20,28)}, {"10", (80,85)},
            {"11", (73,73)}, {"12", (78,79)}, {"13", (79,79)}, {"14", (79,79)}, {"15", (79,79)},
            {"16", (72,79)}, {"17", (79,79)}, {"18", (78,79)}, {"19", (70,74)}, {"20", (81,83)},
            {"21", (75,77)}, {"22", (46,49)}, {"23", (45,48)}, {"24", (36,39)}, {"25", (36,39)},
            {"27", (40,44)}, {"28", (50,56)}, {"29", (53,59)}, {"30", (40,40)}, {"31", (67,68)},
            {"32", (67,69)}, {"33", (53,67)}, {"34", (53,67)}, {"35", (74,74)}, {"36", (50,56)},
            {"37", (18,19)}, {"99", (90,99)}
        };

        private static readonly HashSet<int> ValidAddressCategories = new HashSet<int> { 1, 2, 3, 4, 5 };

        public static void Validate(CustomerRecord record, RecordValidationResult result)
        {
            int row = record.RowNumber;

            foreach (var addr in record.Addresses)
            {
                // PA-01: Address Line 1 is required
                if (string.IsNullOrWhiteSpace(addr.AddressLine1))
                {
                    ValidationHelper.Add(result, row, "PA", "PA-01", "Address Line 1",
                        "Address Line 1 is required.",
                        FailureOutcome.RejectSegment);
                    continue;
                }

                // PA-01: Combined address lines must be at least 3 characters
                var combined = string.Concat(
                    addr.AddressLine1 ?? "",
                    addr.AddressLine2 ?? "",
                    addr.AddressLine3 ?? "",
                    addr.AddressLine4 ?? "",
                    addr.AddressLine5 ?? "");
                if (combined.Length < 3)
                {
                    ValidationHelper.Add(result, row, "PA", "PA-01", "Address Lines",
                        "Combined address lines must be at least 3 characters.",
                        FailureOutcome.RejectSegment);
                }

                // PA-06: State Code validation
                var stateCode = addr.StateCode?.Trim();
                if (!string.IsNullOrEmpty(stateCode))
                {
                    var sc = stateCode.PadLeft(2, '0');
                    if (!ValidStateCodes.Contains(sc))
                    {
                        ValidationHelper.Add(result, row, "PA", "PA-06", "State Code",
                            $"State Code '{stateCode}' is not a valid code.",
                            FailureOutcome.RejectSegment);
                    }
                    else
                    {
                        // PA-07: PIN Code validation (if provided)
                        ValidatePinCode(row, addr, sc, result);
                    }
                }

                // PA-08: Address Category defaults to 04 if invalid (not a rejection)
                if (addr.AddressCategory.HasValue && !ValidAddressCategories.Contains(addr.AddressCategory.Value))
                {
                    // Silently default — this is not a rejection per spec
                    addr.AddressCategory = 4;
                }
            }
        }

        private static void ValidatePinCode(int row, AddressModel addr, string stateCode,
            RecordValidationResult result)
        {
            if (string.IsNullOrEmpty(addr.PinCode)) return;

            var digitsOnly = new string(addr.PinCode.Where(char.IsDigit).ToArray());
            if (digitsOnly.Length < 6)
            {
                ValidationHelper.Add(result, row, "PA", "PA-07", "PIN Code",
                    $"PIN Code must have exactly 6 digits (found {digitsOnly.Length}).",
                    FailureOutcome.RejectSegment);
                return;
            }

            // Take the relevant 6 digits (same logic as TudfBuilder — last 6)
            var pin6 = digitsOnly.Length > 6
                ? digitsOnly.Substring(digitsOnly.Length - 6)
                : digitsOnly;

            // Last 3 digits must not be 000
            if (pin6.Substring(3) == "000")
            {
                ValidationHelper.Add(result, row, "PA", "PA-07", "PIN Code",
                    "PIN Code last 3 digits must not be 000.",
                    FailureOutcome.RejectSegment);
                return;
            }

            // State code 77 (Foreign) — skip prefix validation
            if (stateCode == "77") return;

            // Validate PIN prefix against state code
            if (StatePinPrefixMap.TryGetValue(stateCode, out var range))
            {
                if (int.TryParse(pin6.Substring(0, 2), out int prefix))
                {
                    if (prefix < range.min || prefix > range.max)
                    {
                        ValidationHelper.Add(result, row, "PA", "PA-07", "PIN Code",
                            $"PIN Code prefix '{prefix:D2}' is not valid for State Code '{stateCode}' (expected {range.min:D2}-{range.max:D2}).",
                            FailureOutcome.RejectSegment);
                    }
                }
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  TL — Account Segment
    // ────────────────────────────────────────────────────────────────
    internal static class AccountValidator
    {
        private static readonly HashSet<int> ValidAccountTypes = new HashSet<int>
        {
            0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,
            21,23,24,25,26,27,28,29,30,31,32,33,34,35,36,
            37,38,39,40,41,42,43,44,45,46,47,
            50,51,52,53,54,55,56,57,58,59,
            61,69,70,71
        };

        private static readonly int[] CreditCardTypes = { 10, 16, 31, 35 };
        private static readonly int[] NoEmiTypes = { 10, 12, 16, 31, 35 };

        public static void Validate(CustomerRecord record, RecordValidationResult result)
        {
            var acct = record.Account;
            int row = record.RowNumber;

            // TL-01: Member Code must not be empty
            if (string.IsNullOrWhiteSpace(acct.CurrentMemberCode))
            {
                ValidationHelper.Add(result, row, "TL", "TL-01", "Member Code",
                    "Member Code is required.", FailureOutcome.RejectRecord);
            }

            // TL-03: Account Number must not be empty
            if (string.IsNullOrWhiteSpace(acct.AccountNumber))
            {
                ValidationHelper.Add(result, row, "TL", "TL-03", "Account Number",
                    "Account Number is required.", FailureOutcome.RejectRecord);
            }

            // TL-04: Account Type must be valid
            if (!ValidAccountTypes.Contains(acct.AccountType))
            {
                ValidationHelper.Add(result, row, "TL", "TL-04", "Account Type",
                    $"Account Type {acct.AccountType:D2} is not a valid code.",
                    FailureOutcome.RejectRecord);
            }

            // TL-05: Ownership Indicator must be 1-5
            if (acct.OwnershipIndicator < 1 || acct.OwnershipIndicator > 5)
            {
                ValidationHelper.Add(result, row, "TL", "TL-05", "Ownership Indicator",
                    "Ownership Indicator must be 1-5.", FailureOutcome.RejectRecord);
            }

            // TL-05A: Ownership 2 (Authorised User) only valid for Account Type 10
            if (acct.OwnershipIndicator == 2 && acct.AccountType != 10)
            {
                ValidationHelper.Add(result, row, "TL", "TL-05A", "Ownership Indicator",
                    "Ownership Indicator 2 (Authorised User) is only valid for Account Type 10 (Credit Card).",
                    FailureOutcome.RejectRecord);
            }

            // TL-08: Date Opened/Disbursed is required
            if (acct.DateOpenedDisbursed == default(DateTime))
            {
                ValidationHelper.Add(result, row, "TL", "TL-08", "Date Opened/Disbursed",
                    "Date Opened/Disbursed is required.", FailureOutcome.RejectRecord);
            }

            // TL-09A: Date of Last Payment >= Date Opened
            if (acct.DateOfLastPayment.HasValue && acct.DateOpenedDisbursed != default(DateTime))
            {
                if (acct.DateOfLastPayment.Value < acct.DateOpenedDisbursed)
                {
                    ValidationHelper.Add(result, row, "TL", "TL-09A", "Date of Last Payment",
                        "Date of Last Payment must be on or after Date Opened/Disbursed.",
                        FailureOutcome.RejectField);
                }
            }

            // TL-10A: Date Closed >= Date Opened
            if (acct.DateClosed.HasValue && acct.DateOpenedDisbursed != default(DateTime))
            {
                if (acct.DateClosed.Value < acct.DateOpenedDisbursed)
                {
                    ValidationHelper.Add(result, row, "TL", "TL-10A", "Date Closed",
                        "Date Closed must be on or after Date Opened/Disbursed.",
                        FailureOutcome.RejectRecord);
                }
            }

            // TL-10B: Date of Last Payment <= Date Closed
            if (acct.DateOfLastPayment.HasValue && acct.DateClosed.HasValue)
            {
                if (acct.DateOfLastPayment.Value > acct.DateClosed.Value)
                {
                    ValidationHelper.Add(result, row, "TL", "TL-10B", "Date of Last Payment",
                        "Date of Last Payment must be on or before Date Closed.",
                        FailureOutcome.RejectRecord);
                }
            }

            // TL-10C: When Date Closed is provided, Current Balance must be zero or negative
            if (acct.DateClosed.HasValue)
            {
                if (acct.CurrentBalance > 0 && !acct.IsCurrentBalanceNegative)
                {
                    ValidationHelper.Add(result, row, "TL", "TL-10C", "Current Balance",
                        "When Date Closed is provided, Current Balance must be zero or negative.",
                        FailureOutcome.RejectRecord);
                }
            }

            // TL-11: Date Reported is required
            if (acct.DateReportedAndCertified == default(DateTime))
            {
                ValidationHelper.Add(result, row, "TL", "TL-11", "Date Reported",
                    "Date Reported and Certified is required.", FailureOutcome.RejectRecord);
            }

            // TL-12: High Credit/Sanctioned Amount >= 0
            if (acct.HighCreditSanctionedAmount < 0)
            {
                ValidationHelper.Add(result, row, "TL", "TL-12", "High Credit/Sanctioned Amt",
                    "High Credit/Sanctioned Amount must be >= 0.", FailureOutcome.RejectRecord);
            }

            // TL-13: Current Balance must not be null/empty (it's a long, always has value,
            // but we check if it was successfully parsed — model uses 0 as default which is valid)
            // This is always satisfied since it's a non-nullable long.

            bool isCC = CreditCardTypes.Contains(acct.AccountType);

            // TL-14A: For non-CC accounts, if DPD > 0 then Amount Overdue must be > 0
            if (!isCC && acct.NumberOfDaysPastDue.HasValue && acct.NumberOfDaysPastDue.Value > 0)
            {
                if (!acct.AmountOverdue.HasValue || acct.AmountOverdue.Value <= 0)
                {
                    ValidationHelper.Add(result, row, "TL", "TL-14A", "Amount Overdue",
                        "Amount Overdue must be > 0 when Days Past Due > 0 for non-credit-card accounts.",
                        FailureOutcome.RejectRecord);
                }
            }

            // TL-15A: For non-CC accounts, if Amount Overdue > 0 then DPD must be > 0
            if (!isCC && acct.AmountOverdue.HasValue && acct.AmountOverdue.Value > 0)
            {
                if (!acct.NumberOfDaysPastDue.HasValue || acct.NumberOfDaysPastDue.Value <= 0)
                {
                    ValidationHelper.Add(result, row, "TL", "TL-15A", "Days Past Due",
                        "Days Past Due must be > 0 when Amount Overdue > 0 for non-credit-card accounts.",
                        FailureOutcome.RejectRecord);
                }
            }

            // TL-15-26: Either DPD or Asset Classification must be provided
            if (!acct.NumberOfDaysPastDue.HasValue && !acct.AssetClassification.HasValue)
            {
                ValidationHelper.Add(result, row, "TL", "TL-15-26", "DPD / Asset Classification",
                    "Either Days Past Due or Asset Classification must be provided.",
                    FailureOutcome.RejectRecord);
            }

            // TL-15-CAP: If DPD > 900, warn (correction happens in builder)
            if (acct.NumberOfDaysPastDue.HasValue && acct.NumberOfDaysPastDue.Value > 900)
            {
                ValidationHelper.Add(result, row, "TL", "TL-15-CAP", "Days Past Due",
                    $"Days Past Due ({acct.NumberOfDaysPastDue.Value}) exceeds 900 and will be capped to 900.",
                    FailureOutcome.RejectField);
            }

            // TL-22A: CFS 17 requires Ownership 3
            if (acct.CreditFacilityStatus.HasValue && acct.CreditFacilityStatus.Value == 17)
            {
                if (acct.OwnershipIndicator != 3)
                {
                    ValidationHelper.Add(result, row, "TL", "TL-22A", "Credit Facility Status",
                        "Credit Facility Status 17 (Guarantee Invoked) requires Ownership Indicator 3 (Guarantor).",
                        FailureOutcome.RejectRecord);
                }
            }

            // TL-36: Credit Limit only for CC types
            if (acct.CreditLimit.HasValue && acct.CreditLimit.Value > 0 && !isCC)
            {
                ValidationHelper.Add(result, row, "TL", "TL-36", "Credit Limit",
                    "Credit Limit must not be reported for non-credit-card account types.",
                    FailureOutcome.RejectField);
            }

            // TL-37: Cash Limit only for CC types
            if (acct.CashLimit.HasValue && acct.CashLimit.Value > 0 && !isCC)
            {
                ValidationHelper.Add(result, row, "TL", "TL-37", "Cash Limit",
                    "Cash Limit must not be reported for non-credit-card account types.",
                    FailureOutcome.RejectField);
            }

            // TL-40: EMI not for types 10, 12, 16, 31, 35
            if (acct.EmiAmount.HasValue && acct.EmiAmount.Value > 0 && NoEmiTypes.Contains(acct.AccountType))
            {
                ValidationHelper.Add(result, row, "TL", "TL-40", "EMI Amount",
                    "EMI Amount must not be reported for this account type.",
                    FailureOutcome.RejectField);
            }

            // TL-40B: EMI must be > 0 if provided (and applicable)
            if (acct.EmiAmount.HasValue && acct.EmiAmount.Value <= 0 && !NoEmiTypes.Contains(acct.AccountType))
            {
                ValidationHelper.Add(result, row, "TL", "TL-40B", "EMI Amount",
                    "EMI Amount must be greater than 0 if provided.",
                    FailureOutcome.RejectField);
            }

            // TL-41: Written-off Amount Total required when CFS is 02, 03, or 04
            var cfs = acct.CreditFacilityStatus;
            if (cfs.HasValue && (cfs.Value == 2 || cfs.Value == 3 || cfs.Value == 4))
            {
                if (!acct.WrittenOffAmountTotal.HasValue || acct.WrittenOffAmountTotal.Value <= 0)
                {
                    ValidationHelper.Add(result, row, "TL", "TL-41", "Written-off Amount Total",
                        "Written-off Amount Total is required (> 0) when Credit Facility Status is 02, 03, or 04.",
                        FailureOutcome.RejectRecord);
                }
            }

            // TL-42: Written-off Principal must not exceed Total
            if (acct.WrittenOffAmountPrincipal.HasValue && acct.WrittenOffAmountTotal.HasValue)
            {
                if (acct.WrittenOffAmountPrincipal.Value > acct.WrittenOffAmountTotal.Value)
                {
                    ValidationHelper.Add(result, row, "TL", "TL-42", "Written-off Principal",
                        "Written-off Principal Amount must not exceed Written-off Amount Total.",
                        FailureOutcome.RejectField);
                }
            }

            // TL-43: Settlement Amount required when CFS is 03, 04, 15, or 16
            if (cfs.HasValue && (cfs.Value == 3 || cfs.Value == 4 || cfs.Value == 15 || cfs.Value == 16))
            {
                if (!acct.SettlementAmount.HasValue || acct.SettlementAmount.Value <= 0)
                {
                    ValidationHelper.Add(result, row, "TL", "TL-43", "Settlement Amount",
                        "Settlement Amount is required (> 0) when Credit Facility Status is 03, 04, 15, or 16.",
                        FailureOutcome.RejectField);
                }
            }

            // TL-47: Income must be > 0 if provided
            if (acct.Income.HasValue && acct.Income.Value <= 0)
            {
                ValidationHelper.Add(result, row, "TL", "TL-47", "Income",
                    "Income must be greater than 0 if provided.",
                    FailureOutcome.RejectField);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  Cross-Segment Validator
    // ────────────────────────────────────────────────────────────────
    internal static class CrossSegmentValidator
    {
        private static readonly HashSet<string> Salutations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MR", "MR.", "MRS", "MRS.", "MS", "MS.", "DR", "DR.", "SHRI", "SHRI.", "SMT", "SMT."
        };

        public static void Validate(CustomerRecord record, RecordValidationResult result,
            DateTime headerDateReported)
        {
            var acct = record.Account;
            int row = record.RowNumber;

            // CROSS-01: For accounts opened on/after 01/06/2007, at least one valid ID or telephone
            if (acct.DateOpenedDisbursed >= new DateTime(2007, 6, 1))
            {
                bool hasValidId = record.Identifications != null &&
                    record.Identifications.Any(id =>
                        id.IdType >= 1 && id.IdType <= 6 || id.IdType == 9 || id.IdType == 10);

                bool hasValidPhone = record.Telephones != null &&
                    record.Telephones.Any(p => !string.IsNullOrWhiteSpace(p.TelephoneNumber));

                if (!hasValidId && !hasValidPhone)
                {
                    ValidationHelper.Add(result, row, "CROSS", "CROSS-01", "ID / Telephone",
                        "For accounts opened on/after June 1, 2007, at least one valid ID or telephone is required.",
                        FailureOutcome.RejectRecord);
                }
            }

            // CROSS-02: DOB must be <= Header Date Reported
            if (record.Name.DateOfBirth.HasValue)
            {
                if (record.Name.DateOfBirth.Value > headerDateReported)
                {
                    ValidationHelper.Add(result, row, "CROSS", "CROSS-02", "Date of Birth",
                        "Date of Birth must be on or before the Header Date Reported.",
                        FailureOutcome.RejectRecord);
                }

                // CROSS-03: DOB must be <= Date of Last Payment
                if (acct.DateOfLastPayment.HasValue &&
                    record.Name.DateOfBirth.Value > acct.DateOfLastPayment.Value)
                {
                    ValidationHelper.Add(result, row, "CROSS", "CROSS-03", "Date of Birth",
                        "Date of Birth must be on or before Date of Last Payment.",
                        FailureOutcome.RejectRecord);
                }

                // CROSS-04: DOB must be <= Date Closed
                if (acct.DateClosed.HasValue &&
                    record.Name.DateOfBirth.Value > acct.DateClosed.Value)
                {
                    ValidationHelper.Add(result, row, "CROSS", "CROSS-04", "Date of Birth",
                        "Date of Birth must be on or before Date Closed.",
                        FailureOutcome.RejectRecord);
                }
            }

            // CROSS-05: Date Opened must be <= Header Date Reported
            if (acct.DateOpenedDisbursed != default(DateTime) &&
                acct.DateOpenedDisbursed > headerDateReported)
            {
                ValidationHelper.Add(result, row, "CROSS", "CROSS-05", "Date Opened/Disbursed",
                    "Date Opened/Disbursed must be on or before the Header Date Reported.",
                    FailureOutcome.RejectRecord);
            }

            // CROSS-06: Account Date Reported must be <= Header Date Reported
            if (acct.DateReportedAndCertified != default(DateTime) &&
                acct.DateReportedAndCertified > headerDateReported)
            {
                ValidationHelper.Add(result, row, "CROSS", "CROSS-06", "Date Reported",
                    "Account Date Reported must be on or before the Header Date Reported.",
                    FailureOutcome.RejectRecord);
            }

            // CROSS-07: Account Date Reported must be within 1 year before Header Date
            if (acct.DateReportedAndCertified != default(DateTime) &&
                headerDateReported != default(DateTime))
            {
                var oneYearBefore = headerDateReported.AddYears(-1);
                if (acct.DateReportedAndCertified < oneYearBefore)
                {
                    ValidationHelper.Add(result, row, "CROSS", "CROSS-07", "Date Reported",
                        "Account Date Reported must be within 1 year before the Header Date Reported.",
                        FailureOutcome.RejectRecord);
                }
            }

            // CROSS-08: At least one valid address must be present
            bool hasValidAddress = record.Addresses != null &&
                record.Addresses.Any(a => !string.IsNullOrWhiteSpace(a.AddressLine1));
            if (!hasValidAddress)
            {
                ValidationHelper.Add(result, row, "CROSS", "CROSS-08", "Address",
                    "At least one valid address segment must be present.",
                    FailureOutcome.RejectRecord);
            }

            // CROSS-09: Single-name borrower must have valid ID or mobile
            if (!string.IsNullOrWhiteSpace(record.Name.FullName))
            {
                var nameWords = record.Name.FullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                // Strip leading salutations
                var meaningfulWords = nameWords.Where(w => !Salutations.Contains(w)).ToList();

                if (meaningfulWords.Count <= 1)
                {
                    bool hasId = record.Identifications != null && record.Identifications.Any(
                        id => !string.IsNullOrWhiteSpace(id.IdNumber));
                    bool hasMobile = record.Telephones != null && record.Telephones.Any(
                        t => t.TelephoneType == "01" && !string.IsNullOrWhiteSpace(t.TelephoneNumber));

                    if (!hasId && !hasMobile)
                    {
                        ValidationHelper.Add(result, row, "CROSS", "CROSS-09", "Single Name",
                            "A single-name borrower must have at least one valid ID or a valid mobile number.",
                            FailureOutcome.RejectRecord);
                    }
                }
            }
        }
    }
}

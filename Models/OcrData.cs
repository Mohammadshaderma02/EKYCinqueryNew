namespace EkycInquiry.Models
{
    public class OcrData
    {
        public class CustomValidation
        {
            public bool? validSex { get; set; }
            public bool? validExpiryDate { get; set; }
            public bool? validDateOfBirth { get; set; }
            public bool? symbolBackDetected { get; set; }
            public bool? symbolFrontDetected { get; set; }
            public bool? validDocumentNumber { get; set; }
            public double? symbolBackConfidence { get; set; }
            public double? symbolFrontConfidence { get; set; }
            public double? symbolPetraConfidence { get; set; }
            public double? symbolStarsConfidence { get; set; }
            public bool? validUserPersonalNumber {  get; set; }
        }

        public class Root
        {
            public string? uuid { get; set; }
            public Visual? visual { get; set; }
            public bool? expired { get; set; }
            public string? mrzCode { get; set; }
            public double? userAge { get; set; }
            public string? userSex { get; set; }
            public Validation? validation { get; set; }
            public string? userPicture { get; set; }
            public string? userSurname { get; set; }
            public string? documentType { get; set; }
            public string? userSignature { get; set; }
            public string? documentNumber { get; set; }
            public string? userBloodGroup { get; set; }
            public string? userGivenNames { get; set; }
            public string? userDateOfBirth { get; set; }
            public string userNationality { get; set; }
            public CustomValidation? customValidation { get; set; }
            public string? icaoDocumentType { get; set; }
            public string? localUserParents { get; set; }
            public string? userPlaceOfBirth { get; set; }
            public string? localDocumentType { get; set; }
            public string? icaoDocumentFormat { get; set; }
            public string? userPersonalNumber { get; set; }
            public string? localUserGivenNames { get; set; }
            public string? documentPlaceOfIssue { get; set; }
            public string? userResidenceAddress { get; set; }
            public bool? validForExpatriation { get; set; }
            public string? documentExpirationDate { get; set; }
            public string? documentIssuingCountry { get; set; }
            public string? userCountryOfResidence { get; set; }
            public string? documentIssuingAuthority { get; set; }
            public string? userCivilRegistrationPlace { get; set; }
            public string? localDocumentIssuingCountry { get; set; }
            public string? userCivilRegistrationNumber { get; set; }
            public string? localDocumentIssuingAuthority { get; set; }
            public string? sex { get; set; }
            public string? code { get; set; }
            public string? text { get; set; }
            public string? code1 { get; set; }
            public string? format { get; set; }
            public string? surname { get; set; }
            public string? givenNames { get; set; }
            public DateOfBirth? dateOfBirth { get; set; }
            public string? nationality { get; set; }
            public ExpirationDate? expirationDate { get; set; }
            public string? issuingCountry { get; set; }
            public bool? validComposite { get; set; }
            public bool? validDateOfBirth { get; set; }
            public bool? validDocumentNumber { get; set; }
            public bool? validExpirationDate { get; set; }

        }

        public class Validation
        {
            public bool? validSize { get; set; }
            public bool? validLight { get; set; }
            public bool? validColors { get; set; }
            public bool? validNumber { get; set; }
            public bool? validTexture { get; set; }
            public double? ocrConfidence { get; set; }
            public bool? validDocument { get; set; }
            public bool? validMrzComposite { get; set; }
            public bool? validMrzDateOfBirth { get; set; }
            public bool? validMrzDateOfExpiry { get; set; }
            public bool? validMrzDocumentNumber { get; set; }
        }

        public class Visual
        {
            public string? sex { get; set; }
            public string? dateOfBirth { get; set; }
            public string? documentNumber { get; set; }
            public string? expirationDate { get; set; }
            public string? userNames { get; set; }
            public string? bloodLabel { get; set; }
            public string? frontHeaderLeftText { get; set; }
            public string? frontHeaderRightText { get; set; }
            public double? frontHeaderLeftTextRows { get; set; }
            public double? frontHeaderRightTextRows { get; set; }
            public string? colorBackAreaRGB {  get; set; }
            public string? colorFrontAreaRGB {  get; set; }
            public string? colorBackAreaRGBExpected {  get; set; }
            public string? colorFrontAreaRGBExpected { get; set; }
            public double? colorFrontAreaRGBMean {  get; set; }
            public double? colorBackAreaRGBMean { get; set; }
            public string? userPersonalNumber { get; set; }
        }

        public class DateOfBirth
        {
            public int? day { get; set; }
            public int? year { get; set; }
            public int? month { get; set; }
        }

        public class ExpirationDate
        {
            public int? day { get; set; }
            public int? year { get; set; }
            public int? month { get; set; }
        }


    }
}

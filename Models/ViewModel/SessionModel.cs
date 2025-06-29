namespace EkycInquiry.Models.ViewModel
{
    public class SessionModel
    {
        public Session Session { get; set; }
        public Line Line { get; set; }
        public List<Medium> Media { get; set; }

        public List<string> Audits { get; set; }
        public List<SessionStep> SessionSteps { get; set; }
        public Mrzblacklist BlacklistHistory { get; set; }


        
    }

    public class TRCSessionModel
    {
        public List<TRCSession> Sessions { get; set; }
        public string StringSessions { get; set; }

        public int NumberOfActivatedLines { get; set; }
        public int NumberOfActivatedLinesJordanian { get; set; }
        public int NumberOfActivatedLinesNonJordanian { get; set; }
        public int NumberOfSanadActivatedLines {  get; set; }
        public int NumberOfFailedActivations { get; set; }
        public int NumberOfFailedActivationsPerSession {  get; set; }
        public int NumberOfFailedActivationsPerNationalNumberOrDocumentNumber {  get; set; }
        public int NumberOfActivatedLinesToday { get; set; }
        public int NumberOfActivatedSanadLinesToday { get; set; }
        public int NumberOfFailedActivationsPerSessionToday { get; set; }
        public int NumberOfFailedActivationsPerNationalNumberOrDocumentNumberToday { get; set; }

    }

    public class TRCSession
    {
        public string NationalID { get; set; }
        public string FullName { get; set; }
        public string DateOfBirth { get; set; }
        public string Status { get; set; }
        public string FailureReason { get; set; }
        public string ActivationType { get; set; }
        public string SelectedPackage { get; set; }
        public string Simcard {  get; set; }
        public string MSISDN { get; set; }
        public string ActivationDate { get; set; }
        public string CurrentStep {  get; set; }
        public string SessionID {  get; set; }
        public string status {  get; set; }
        public string PersonalNumber {  get; set; }
        public string AccuraResult { get; set; }
        public string AccuraMessage { get; set; }
        public long ActivationDateTicks { get; set; }
        public bool IsJordanian { get; set; }
        public string Nationality { get; set; }
        public string Channel {  get; set; }
    }
}

namespace EkycInquiry.Models.ViewModel
{
    public class LoadAllDataQueryModel
    {
        public int id { get; set; }
        public string uid { get; set; }
        public string? JORDANIAN_NAME { get; set; }
        public string? JORDANIAN_NATIONAL_ID { get; set; }
        public string? NON_JORDANIAN_NATIONALITY { get; set; }
        public string? DOCUMENT_NUMBER { get; set; }
        public string? NON_JORDANIAN_FIRST_NAME { get; set; }
        public string? NON_JORDANIAN_SURNAME { get; set; }
        public string? LATEST_SESSION_STEP_UID { get; set; }
        public string? FACE_MATCH_THRESHOLD { get; set; }
        public string? LIVENESS_THRESHOLD { get; set; }
        public int TOKEN_ID { get; set; }
        //public string? ACCURA_RESULT {get; set;}
        //public string? ACCURA_MESSAGE { get; set; }
        public DateTime creationTime { get; set; }
        public string status { get; set; }
    }
}

namespace EkycInquiry.Models.ViewModel
{
    public class TRCReportGetAllDataQueryModel
    {
        public int id { get; set; }
        public string uid { get; set; }
        public string? JORDANIAN_NAME { get; set; }
        public string? JORDANIAN_NATIONAL_ID { get; set; }
        public string? NON_JORDANIAN_NATIONALITY { get; set; }
        public string? DOCUMENT_NUMBER { get; set; }
        public string? NON_JORDANIAN_FIRST_NAME { get; set; }
        public string? NON_JORDANIAN_SURNAME { get; set; }
        public DateTime CREATION_TIME { get; set; }
        public string? STATUS { get; set; }
        public string? KIT_CODE { get; set; }
        public string? PASSPORT_BARCODE { get; set; }
        public string? LINE_NATIONAL_ID { get; set; }
        public string? FLOW {  get; set; }
        public string? MARKET_TYPE {  get; set; }
        public string? JORDANIAN_DOB {  get; set; }
        public string? NON_JORDANIAN_DOB { get; set; }
        public string? SIM_CARD {  get; set; }
        public string? MSISDN { get; set; }
        public string? LATEST_SESSION_STEP_UID { get; set; }
        public string? CHANNEL_USER {  get; set; }
        //public string? ACCURA_RESULT {  get; set; }
        //public string? ACCURA_MESSAGE { get; set; }
    }
}

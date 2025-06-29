namespace EkycInquiry.Models.ViewModel
{
    public class SessionsTimeSeriesReport
    {
        public DateTime session_date {  get; set; }
        public int approved_count { get; set; }
        public int approval_pending_count { get; set; }
        public int to_discard_count { get; set; }
        public int working_count { get; set; }
        public int total_count { get; set; }
    }
}

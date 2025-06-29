namespace EkycInquiry.Models.ViewModel
{
    public class SearchViewModel
    {
        public string SeachValue { get; set; }
        public string SearchCategory { get; set; }

        public Session Result { get; set; }
        public bool IsFound {  get; set; }
    }
}

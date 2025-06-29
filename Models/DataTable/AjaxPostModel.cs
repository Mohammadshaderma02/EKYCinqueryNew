namespace EkycInquiry.Models.DataTable
{
    public class AjaxPostModel
    {
        public int draw { get; set; }
        public int start { get; set; }
        public int length { get; set; }
        public List<Column> columns { get; set; }
        public Search search { get; set; }
        public List<Order> order { get; set; }
    }

    public class AjaxResponseModel
    {
        public string[][] data { get; set; }
        public int draw { get; set; }
        public string recordsTotal { get; set; }
        public string recordsFiltered { get; set; }
    }

}

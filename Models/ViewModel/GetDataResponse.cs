namespace EkycInquiry.Models.ViewModel
{
    public class GetDataResponse<T>
    {
        public int Status { get; set; }
        public T data { get; set; }
    }
}

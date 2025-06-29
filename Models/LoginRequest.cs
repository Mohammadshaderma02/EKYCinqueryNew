namespace EkycInquiry.Models
{
    public class LoginRequest
    {
        public string? Username { get; set; } 
        public string? Password { get; set; }
        public string? ErrorMessage { get; set; }

        public LoginRequest()
        {
            ErrorMessage = "";
        }
    }

}

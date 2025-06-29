using EkycInquiry.Models;
using System.Text;

namespace EkycInquiry.Services
{
    public class HRSoapClient
    {
        private readonly HRSoap.HRAPISoapClient Client = new HRSoap.HRAPISoapClient(HRSoap.HRAPISoapClient.EndpointConfiguration.HRAPISoap);
        public async Task<HRSoap.LoginData> GetUserDetails(LoginRequest user)
        {
            var passwordBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.Password!));

            var response = Client.HRLoginNew(new HRSoap.HRLoginNewRequest
            {
                Body = new HRSoap.HRLoginNewRequestBody
                {
                    Username = user.Username,
                    Password = passwordBase64,
                }
            });

            return response.Body.HRLoginNewResult;
        }
    }
}

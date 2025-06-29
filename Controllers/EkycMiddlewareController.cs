using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EkycInquiry.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class EkycMiddlewareController : ControllerBase
    {
        [HttpGet]
        public IActionResult InitiateSession()
        {
            return Ok(new
            {
                Status = "OK",
                SessionID = Guid.NewGuid().ToString("N")
            });
        }

        //[HttpPost]
        //public IActionResult GetOCRData(IFormFile Front, IFormFile Back)
        //{

        //}
    }
}

using EkycInquiry.Models;
using EkycInquiry.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Security.Claims;

namespace EkycInquiry.Controllers
{
    public class LoginController : Controller
    {
        private readonly IConfiguration _config;
        private readonly List<string> _whitelistedUsers;

        public LoginController(IConfiguration config)
        {
            _config = config;
            _whitelistedUsers = new List<string>();
            _config.GetSection("WhitelistedUsers").Bind(_whitelistedUsers);
        }

        public IActionResult Index()
        {
            return View(new LoginRequest());
        }

        [HttpPost]
        public async Task<IActionResult> Index(LoginRequest user)
        {
            try
            {
                if (user.Username != "en_user")
                {
                    HRSoapClient client = new HRSoapClient();
                    HRSoap.LoginData HrApiResult = await client.GetUserDetails(user);

                    if (!string.IsNullOrEmpty(HrApiResult.PFNumber))
                    {
                        if (_whitelistedUsers.Contains(HrApiResult.Email.ToLower()!) && await CreateCookie(user))
                        {
                            //StaticHelpers.Log($"User {user.Username} has logged in to the dashboard at {DateTime.Now}");
                            return RedirectToAction("Index", "Home");
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "User is not authorized to access this site.";
                            return RedirectToAction("Index", "Login");
                        }
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Invalid credentials. Please check your username or password.";
                        return RedirectToAction("Index", "Login");
                    }
                }
                else
                {
                    if (user.Password == "0=@1D{uk1dTY")
                    {
                        await CreateCookie(user);
                        //StaticHelpers.Log($"User {user.Username} has logged in to the dashboard at {DateTime.Now}");
                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Invalid credentials. Please check your username or password.";
                        return RedirectToAction("Index", "Login");
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An unknown error occurred. Please check your connection.";
                return RedirectToAction("Index", "Login");
            }
        }

        private async Task<bool> CreateCookie(LoginRequest user)
        {
            try
            {
                var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, "Administrator"),
                    };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {

                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15),

                    IsPersistent = true,

                    AllowRefresh = true

                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

                return true;

            }
            catch (Exception ex)
            {
                user.ErrorMessage = ex.Message;
                return false;

            }

        }
        public async Task<IActionResult> Signout()
        {
            try
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Index", "Login");
            }
            catch (Exception ex)
            {

                return RedirectToAction("Index", "Login");
            }
        }



    }
}

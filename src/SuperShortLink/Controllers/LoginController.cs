using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace SuperShortLink
{
    public class LoginController : Controller
    {
        private readonly AdminCredentialService _credentials;

        public LoginController(AdminCredentialService credentials)
        {
            _credentials = credentials;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [EnableRateLimiting("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string name, string password)
        {
            if (_credentials.Verify(name?.Trim(), password))
            {
                var claims = new[] { new Claim(ClaimTypes.Name, _credentials.UserName),
                    new Claim(ClaimTypes.Role, "Administrator"), new Claim("credential-version", _credentials.Version) };
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
                return Redirect("/Home/Dashboard");
            }

            ViewData["Error"] = "用户名或密码错误";
            return View(nameof(Index));
        }

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword() => View();

        [Authorize]
        [HttpPost]
        [EnableRateLimiting("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 12)
                ModelState.AddModelError(string.Empty, "新密码至少需要 12 位");
            else if (newPassword != confirmPassword)
                ModelState.AddModelError(string.Empty, "两次输入的新密码不一致");
            else if (newPassword == currentPassword)
                ModelState.AddModelError(string.Empty, "新密码不能与旧密码相同");

            if (!ModelState.IsValid) return View();
            if (!_credentials.ChangePassword(currentPassword, newPassword))
            {
                ModelState.AddModelError(string.Empty, "当前密码错误");
                return View();
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Index), new { passwordChanged = 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Index));
        }
    }
}

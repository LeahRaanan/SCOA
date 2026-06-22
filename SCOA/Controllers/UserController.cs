using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using SCOA.Models;
using SCOA.Services;
using BLL;

namespace SCOA.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly BLLService _bll;
        private readonly SecurityService _security;
        private readonly IConfiguration _config;

        public UserController(BLLService bll, SecurityService security, IConfiguration config)
        {
            _bll = bll;
            _security = security;
            _config = config;
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (request == null) return BadRequest("נתוני משתמש ריקים.");

                // בדיקת כפילות דרך ה-BLL
                var existingUser = _bll.GetUserByEmail(request.Email);
                if (existingUser != null) return Conflict("המשתמש כבר קיים במערכת.");

                // קריאה ללוגיקה ב-BLL
                var newUserId = _bll.RegisterUser(request);

                return Ok(new { message = "המשתמש נרשם בהצלחה!", userId = newUserId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"שגיאה פנימית בשרת: {ex.Message}");
            }
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null) return BadRequest("נתוני התחברות ריקים.");

            var user = _bll.GetUserByEmail(request.Email);
            if (user == null) return NotFound(new { message = "משתמש לא נמצא." });

            if (!_security.VerifyPassword(request.Password, user.Password))
                return Unauthorized(new { message = "סיסמה שגויה." });

            var token = GenerateJwtToken(user);
            return Ok(new { token, userId = user.Id, userName = user.UserName, user.InterestScores });
        }




        // ════════════════════════════════════════════════════════════════════
        //  JWT Helper
        // ════════════════════════════════════════════════════════════════════

        private string GenerateJwtToken(User user)
        {
            var jwtKey = _config["Jwt:Key"]!;
            var jwtIssuer = _config["Jwt:Issuer"]!;

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name,           user.UserName)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtIssuer,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("send_emails")]
        public IActionResult SendTestEmails()
        {
            try
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await _bll.SendDailyRecommendedEmails();
                        Console.WriteLine(" [Success] תהליך שליחת המיילים הידני הסתיים בהצלחה.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" [Error] שגיאה ברקע בזמן שליחת מיילים: {ex.Message}");
                    }
                });

                return Ok(new { message = "תהליך שליחת המיילים הופעל ומבוצע כעת ברקע עבור כל המשתמשים" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"שגיאה בהפעלת תהליך המיילים: {ex.Message}" });
            }
        }

        [Authorize]
        [HttpPut("profile")]
        public IActionResult UpdateProfile([FromBody] UpdateProfileDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            _bll.UpdateUserProfile(userId, request.UserName, request.ChosenCategories, request.PreferredTimeRange);

            return Ok(new { message = "Profile updated successfully." });
        }

        [Authorize]
        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = _bll.GetUserById(userId!);

            if (user == null) return NotFound();

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.ChosenCategories,
                user.PreferredTimeRange,
                user.InterestScores,
                user.CreatedAt
            });
        }
    }
}
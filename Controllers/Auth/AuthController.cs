using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Web.Http;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace task_full_stack.Controllers.auth
{
    public class AuthController : ApiController
    {
        MovingRelocationDBEntities db = new MovingRelocationDBEntities();

        // Config values - appsettings/web.config se lena best practice hai
        private readonly string _jwtSecret = "THIS_IS_A_LONG_SECRET_KEY_CHANGE_ME_32CHARS+";
        private readonly string _issuer = "MovingRelocationApp";
        private readonly int _accessTokenMinutes = 15;
        private readonly int _refreshTokenDays = 7;

        // POST api/Auth/login
        [HttpPost]
        [Route("api/Auth/login")]
        public IHttpActionResult Login([FromBody] LoginDto model)
        {
            try
            {
                if (model == null ||
                    string.IsNullOrWhiteSpace(model.Email) ||
                    string.IsNullOrWhiteSpace(model.Password))
                {
                    return BadRequest("Email and Password are required.");
                }

                var user = db.Users.FirstOrDefault(u =>
                    u.Email == model.Email &&
                    u.IsDeleted == false);

                if (user == null)
                {
                    return Content(HttpStatusCode.Unauthorized,
                        new { message = "Invalid email or password." });
                }

                var hashedInput = HashPassword(model.Password);

                if (user.PasswordHash != hashedInput)
                {
                    return Content(HttpStatusCode.Unauthorized,
                        new { message = "Invalid email or password." });
                }

                var roleName = db.Roles
                    .Where(r => r.Id == user.RoleId)
                    .Select(r => r.Name)
                    .FirstOrDefault();

                var accessToken = GenerateAccessToken(user, roleName);
                var refreshToken = GenerateRefreshTokenString();

                var oldTokens = db.RefreshTokens
                    .Where(rt => rt.UserId == user.Id && rt.IsRevoked == false)
                    .ToList();

                foreach (var t in oldTokens)
                {
                    t.IsRevoked = true;
                }

                var refreshEntity = new RefreshToken
                {
                    UserId = user.Id,
                    Token = refreshToken,
                    ExpiryDate = DateTime.Now.AddDays(_refreshTokenDays),
                    IsRevoked = false
                };

                db.RefreshTokens.Add(refreshEntity);
                db.SaveChanges();

                return Ok(new
                {
                    accessToken = accessToken,
                    refreshToken = refreshToken,
                    expiresIn = _accessTokenMinutes * 60,
                    user = new
                    {
                        user.Id,
                        user.FirstName,
                        user.LastName,
                        user.Email,
                        user.CompanyId,
                        user.BranchId,
                        Role = roleName
                    }
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Auth/refresh-token
        [HttpPost]
        [Route("api/Auth/refresh-token")]
        public IHttpActionResult RefreshToken([FromBody] RefreshTokenDto model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.RefreshToken))
                    return BadRequest("Refresh token is required.");

                var storedToken = db.RefreshTokens.FirstOrDefault(rt => rt.Token == model.RefreshToken);

                if (storedToken == null || storedToken.IsRevoked == true || storedToken.ExpiryDate < DateTime.Now)
                    return Content(HttpStatusCode.Unauthorized, new { message = "Invalid or expired refresh token." });

                var user = db.Users.FirstOrDefault(u => u.Id == storedToken.UserId && u.IsDeleted == false);
                if (user == null)
                    return Content(HttpStatusCode.Unauthorized, new { message = "User not found." });

                var roleName = db.Roles.Where(r => r.Id == user.RoleId).Select(r => r.Name).FirstOrDefault();

                // Old token revoke, naya generate (rotation)
                storedToken.IsRevoked = true;

                var newAccessToken = GenerateAccessToken(user, roleName);
                var newRefreshToken = GenerateRefreshTokenString();

                var refreshEntity = new RefreshToken
                {
                    UserId = user.Id,
                    Token = newRefreshToken,
                    ExpiryDate = DateTime.Now.AddDays(_refreshTokenDays),
                    IsRevoked = false
                };

                db.RefreshTokens.Add(refreshEntity);
                db.SaveChanges();

                return Ok(new
                {
                    accessToken = newAccessToken,
                    refreshToken = newRefreshToken,
                    expiresIn = _accessTokenMinutes * 60
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/Auth/logout
        [HttpPost]
        [Route("api/Auth/logout")]
        public IHttpActionResult Logout([FromBody] RefreshTokenDto model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.RefreshToken))
                    return BadRequest("Refresh token is required.");

                var storedToken = db.RefreshTokens.FirstOrDefault(rt => rt.Token == model.RefreshToken);

                if (storedToken == null)
                    return NotFound();

                storedToken.IsRevoked = true;
                db.SaveChanges();

                return Ok(new { message = "Logged out successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ================= HELPERS =================

        private string GenerateAccessToken(User user, string roleName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim("CompanyId", user.CompanyId.ToString()),
                new Claim("BranchId", user.BranchId?.ToString() ?? ""),
                new Claim(ClaimTypes.Role, roleName ?? "")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _issuer,
                claims: claims,
                expires: DateTime.Now.AddMinutes(_accessTokenMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshTokenString()
        {
            var randomBytes = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder();
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class RefreshTokenDto
    {
        public string RefreshToken { get; set; }
    }
}
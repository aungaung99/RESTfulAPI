using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

using RESTfulAPI.Entities;
using RESTfulAPI.Model;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RESTfulAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly IConfiguration _configuration;
        private readonly VirtualDBContext context;

        public AuthController(UserManager<IdentityUser> userManager, IConfiguration configuration, VirtualDBContext context)
        {
            this.userManager = userManager;
            _configuration = configuration;
            this.context = context;
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> Register([FromForm] RegisterModel model)
        {
            IdentityUser userExists = await userManager.FindByNameAsync(model.Username);
            if (userExists != null)
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseModel { Error = new { message = "This user is already exist." }, Data = model });

            IdentityUser user = new()
            {
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Username
            };

            IdentityResult result = await userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, model.Role);
                UserInfo userInfo = new()
                {
                    UserId = user.Id,
                    JoinDate = DateTime.Now
                };
                context.UserInfos.Add(userInfo);
                await context.SaveChangesAsync();
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ResponseModel { Meta = model, Error = new { message = result.Errors } });
            }

            return Ok(new ResponseModel { Meta = model, Data = user });
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("access-token")]
        public async Task<IActionResult> Login([FromForm] LoginModel model)
        {
            IdentityUser user = await userManager.FindByNameAsync(model.Username);
            if (user != null && await userManager.CheckPasswordAsync(user, model.Password))
            {
                string access_token = await GenerateAccessToken(user);
                string refresh_token = GenerateRefreshToken();

                UserInfo userInfo = await context.UserInfos.FindAsync(user.Id);
                if (userInfo != null)
                {
                    userInfo.RefreshToken = refresh_token;
                    userInfo.TokenExpired = DateTime.Now.AddDays(1);
                    await context.SaveChangesAsync();
                }

                return Ok(new ResponseModel
                {
                    Meta = model,
                    Data = new
                    {
                        access_token,
                        refresh_token,
                        expiration = DateTime.Now.AddDays(1),
                        user = new
                        {
                            user.Id,
                            user.Email,
                            user.UserName,
                            user.PhoneNumber,
                            userInfo.FullName,
                            userInfo.Dob,
                            userInfo.JoinDate,
                        }
                    }
                });
            }
            return Unauthorized();
        }

        [HttpPost]
        [Route("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromForm] JWTTokenModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ResponseModel { Error = new { message = "Invalid client request" } });
            }

            string accessToken = model.Access_Token;
            string refreshToken = model.Refresh_Token;

            // Check the access token is valid
            ClaimsPrincipal principal = GetPrincipalFromExpiredToken(accessToken);
            if (principal == null)
            {
                return BadRequest(new ResponseModel { Data = model, Error = new { message = "Invalid access token or refresh token" } });
            }

            string username = principal.Identity.Name;
            IdentityUser user = await userManager.FindByNameAsync(username);
            var userInfo = await context.UserInfos.FindAsync(user.Id);
            if (userInfo == null || userInfo.RefreshToken != refreshToken || userInfo.TokenExpired <= DateTime.Now)
            {
                return BadRequest(new ResponseModel { Data = model, Error = new { message = "Invalid access token or refresh token" } });
            }


            string access_token = await GenerateAccessToken(user);
            string refresh_token = GenerateRefreshToken();

            userInfo.RefreshToken = refresh_token;
            userInfo.TokenExpired = DateTime.Now.AddDays(1);
            await context.SaveChangesAsync();

            return Ok(new ResponseModel
            {
                Meta = "Successfully Generate Refresh Token",
                Data = new
                {
                    access_token,
                    refresh_token,
                    expiration = DateTime.Now.AddDays(1),
                    user = new
                    {
                        user.Id,
                        user.Email,
                        user.UserName,
                        user.PhoneNumber,
                        userInfo.FullName,
                        userInfo.Dob,
                        userInfo.JoinDate,
                    }
                }
            });
        }

        [Authorize]
        [HttpPost]
        [Route("revoke-token/{username}")]
        public async Task<IActionResult> Revoke(string username)
        {
            IdentityUser user = await userManager.FindByNameAsync(username);
            if (user == null) return BadRequest("Invalid user name");

            UserInfo userInfo = await context.UserInfos.FindAsync(user.Id);
            if (userInfo != null)
            {
                userInfo.RefreshToken = null;
                userInfo.TokenExpired = null;
            }

            await context.SaveChangesAsync();

            return Ok(new ResponseModel { Meta = "username", Data = "Token Revoke Successfully" });
        }

        [NonAction]
        public async Task<string> GenerateAccessToken(IdentityUser user)
        {
            IList<string> userRoles = await userManager.GetRolesAsync(user);

            List<Claim> authClaims = new()
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            foreach (string userRole in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, userRole));
            }

            SymmetricSecurityKey authSigningKey = new(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));
            JwtSecurityToken token = new(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddSeconds(30),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [NonAction]
        public string GenerateRefreshToken()
        {
            byte[] randomNumber = new byte[64];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        [NonAction]
        private ClaimsPrincipal GetPrincipalFromExpiredToken(string access_token)
        {

            TokenValidationParameters tokenValidationParameters = new()
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"])),
                ValidateLifetime = false,
            };

            JwtSecurityTokenHandler tokenHandler = new();
            ClaimsPrincipal principal = tokenHandler.ValidateToken(access_token, tokenValidationParameters, out SecurityToken securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;

        }
    }
}

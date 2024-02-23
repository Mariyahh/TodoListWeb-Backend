using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoListWeb.Data;
using TodoListWeb.Model;
using TodoListWeb.DTO;
using BCrypt.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace TodoListWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly TodoListWebContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _jwtSecretKey;


        public UserController(TodoListWebContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _jwtSecretKey = _configuration["JwtSettings:SecretKey"] ?? GenerateSecretKey();
        }

        // GET: api/User
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Users>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        // GET: api/User/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Users>> GetUsers(int id)
        {
            var users = await _context.Users.FindAsync(id);

            if (users == null)
            {
                return NotFound();
            }

            return users;
        }

        // PUT: api/User/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUsers(int id, Users users)
        {
            if (id != users.Id)
            {
                return BadRequest();
            }

            _context.Entry(users).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UsersExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/User
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Users>> PostUsers(Users users)
        {
            _context.Users.Add(users);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUsers", new { id = users.Id }, users);
        }

        // Method to generate a random password salt
        private byte[] GeneratePasswordSalt()
        {
            // Generate a random salt value
            var salt = BCrypt.Net.BCrypt.GenerateSalt();

            // Log the generated salt
            Console.WriteLine($"Generated Salt: {salt}");

            return Encoding.UTF8.GetBytes(salt); // Convert the salt string to byte array
        }

        // Method to hash password using salt
        private string HashPassword(string password, byte[] salt)
        {
            // Convert the byte array salt to a string
            var saltString = Encoding.UTF8.GetString(salt);

            // use BCrypt.Net to hash the password with the salt
            return BCrypt.Net.BCrypt.HashPassword(password, saltString);
        }

        // POST: api/User/Register
        [HttpPost("Register")]
        public async Task<ActionResult<Users>> Register(UserRegistrationDto model)
        {
            try
            {
                // Validate the input data
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if the username or email already exists
                if (await UserExists(model.Username, model.Email))
                {
                    return Conflict("Username or email already exists");
                }

                // Map DTO to entity
                var user = new Users
                {
                    Username = model.Username,
                    Email = model.Email,
                };

                // Generate and set the password salt
                var passwordSalt = GeneratePasswordSalt();
                user.PasswordSalt = passwordSalt;

                // Hash pass
                var passwordHash = HashPassword(model.Password, passwordSalt);
                user.PasswordHash = passwordHash;

                // Save the user data to the database
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetUsers", new { id = user.Id }, user);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Register method: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }

        private async Task<bool> UserExists(string username, string email)
        {
            return await _context.Users.AnyAsync(u => u.Username == username || u.Email == email);
        }

        // POST: api/User/Login
        [HttpPost("Login")]
        public async Task<ActionResult<string>> Login(UserLoginDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null || !VerifyPassword(model.Password, user.PasswordHash))
            {
                return Unauthorized("Invalid email or password");
            }

            // Generate JWT token
            var token = GenerateJwtToken(user);

            return Ok(new { token });
        }

        // generate a random secret key
        private string GenerateSecretKey()
        {
            var randomNumber = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomNumber);
            }
            return Convert.ToBase64String(randomNumber);
        }

        private string GenerateJwtToken(Users user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSecretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim("userId", user.Id.ToString()),
                }),
                Expires = DateTime.UtcNow.AddHours(1), // Token expiration 
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private bool VerifyPassword(string password, string passwordHash)
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }

        // POST: api/User/Logout
        [HttpPost("Logout")]
        public IActionResult Logout()
        {
            // Retrieve the JWT token from haeders
            var token = HttpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (token == null)
            {
                return BadRequest("Token is missing");
            }

            // Add the token to the blacklist (para logout)
            BlacklistToken(token);

            return Ok("Logout successful");
        }

        // blacklist
        private void BlacklistToken(string token)
        {
            // if the token is not blacklisted
            if (!_context.BlacklistedTokens.Any(bt => bt.Token == token))
            {
                var blacklistedToken = new BlacklistedToken
                {
                    Token = token,
                    BlacklistedAt = DateTime.UtcNow
                };

                // Add to db
                _context.BlacklistedTokens.Add(blacklistedToken);
                _context.SaveChanges();
            }
        }

        // DELETE: api/User/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUsers(int id)
        {
            var users = await _context.Users.FindAsync(id);
            if (users == null)
            {
                return NotFound();
            }

            _context.Users.Remove(users);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool UsersExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}

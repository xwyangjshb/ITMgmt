using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ITDeviceManager.API.Services;
using ITDeviceManager.API.Data;
using ITDeviceManager.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ITDeviceManager.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly DeviceContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, DeviceContext context, ILogger<AuthController> logger)
        {
            _authService = authService;
            _context = context;
            _logger = logger;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { error = "Username and password are required" });
            }

            var token = await _authService.AuthenticateAsync(request.Username, request.Password);

            if (token == null)
            {
                return Unauthorized(new { error = "Invalid username or password" });
            }

            return Ok(new LoginResponse
            {
                Token = token,
                Username = request.Username
            });
        }

        [HttpPost("register")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<ActionResult<User>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Check if username already exists
                var existingUser = await _context.Set<User>()
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                if (existingUser != null)
                {
                    return Conflict(new { error = "Username already exists" });
                }

                // Validate role
                if (!IsValidRole(request.Role))
                {
                    return BadRequest(new { error = "Invalid role specified" });
                }

                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    Role = request.Role ?? UserRoles.User,
                    PasswordHash = _authService.HashPassword(request.Password),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Set<User>().Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New user '{Username}' registered with role '{Role}'", user.Username, user.Role);

                // Return user without password hash
                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.Role,
                    user.IsActive,
                    user.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user '{Username}'", request.Username);
                return StatusCode(500, new { error = "Registration failed", message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Set<User>().FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            // Return without password hash
            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt,
                user.LastLoginAt
            });
        }

        [HttpGet("users")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<ActionResult<IEnumerable<object>>> GetUsers()
        {
            var users = await _context.Set<User>()
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt,
                    u.LastLoginAt
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("init")]
        [AllowAnonymous]
        public async Task<ActionResult> InitializeDefaultAdmin()
        {
            try
            {
                // Check if any admin users exist
                var adminExists = await _context.Set<User>()
                    .AnyAsync(u => u.Role == UserRoles.Admin);

                if (adminExists)
                {
                    return BadRequest(new { error = "Admin user already exists" });
                }

                // Create default admin user
                var admin = new User
                {
                    Username = "admin",
                    Email = "admin@itdevicemanager.local",
                    Role = UserRoles.Admin,
                    PasswordHash = _authService.HashPassword("Admin@123"),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Set<User>().Add(admin);
                await _context.SaveChangesAsync();

                _logger.LogWarning("Default admin user created. Username: admin, Password: Admin@123 - CHANGE THIS IMMEDIATELY!");

                return Ok(new
                {
                    message = "Default admin user created successfully",
                    username = "admin",
                    warning = "Please change the default password immediately!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing default admin user");
                return StatusCode(500, new { error = "Initialization failed", message = ex.Message });
            }
        }

        private static bool IsValidRole(string? role)
        {
            if (string.IsNullOrEmpty(role))
                return true; // Default role will be used

            return role == UserRoles.Admin ||
                   role == UserRoles.Operator ||
                   role == UserRoles.User;
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Role { get; set; }
    }
}

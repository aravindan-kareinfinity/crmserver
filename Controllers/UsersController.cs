using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    /// <summary>Parity with core-crm-suite <c>UserService.ts</c>.</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>Sign in with email and password. Returns userId, username, refreshToken, role.</summary>
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login([FromBody] LoginRequestDto dto)
        {
            var result = await _userService.Login(dto);
            if (!result.Success)
                return Unauthorized(result);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<UserResponseDto>>>> GetAll()
        {
            var result = await _userService.GetAll();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetById(int id)
        {
            var result = await _userService.GetById(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpGet("email/{email}")]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetByEmail(string email)
        {
            var result = await _userService.GetByEmail(email);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpGet("status/{status}")]
        public async Task<ActionResult<ApiResponse<List<UserResponseDto>>>> GetByStatus(string status)
        {
            var result = await _userService.GetByStatus(status);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> Create(CreateUserDto dto)
        {
            var result = await _userService.CreateUser(dto);
            if (!result.Success) return BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> Update(int id, UpdateUserDto dto)
        {
            var result = await _userService.UpdateUser(id, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _userService.DeleteUser(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}

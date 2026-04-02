using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    /// <summary>Parity with core-crm-suite <c>UserService.ts</c>.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        IUserService userService;

        public UsersController(IUserService userService)
        {
            this.userService = userService;
        }

        /// <summary>Sign in with email and password. Returns userId, username, refreshToken, role.</summary>
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login([FromBody] LoginRequestDto dto)
        {
            var result = await userService.Login(dto);
            if (!result.Success)
                return Unauthorized(result);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<UserResponseDto>>>> GetAll()
        {
            var result = await userService.GetAll();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetById(int id)
        {
            var result = await userService.GetById(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpGet("email/{email}")]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetByEmail(string email)
        {
            var result = await userService.GetByEmail(email);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpGet("status/{status}")]
        public async Task<ActionResult<ApiResponse<List<UserResponseDto>>>> GetByStatus(string status)
        {
            var result = await userService.GetByStatus(status);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> Create(CreateUserDto dto)
        {
            var result = await userService.CreateUser(dto);
            if (!result.Success) return BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<UserResponseDto>>> Update(int id, UpdateUserDto dto)
        {
            var result = await userService.UpdateUser(id, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await userService.DeleteUser(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}

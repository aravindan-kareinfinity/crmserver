using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchedulerEventsController : ControllerBase
    {
        private readonly ISchedulerService _schedulerService;

        public SchedulerEventsController(ISchedulerService schedulerService)
        {
            _schedulerService = schedulerService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<SchedulerEventResponseDto>>>> GetAll()
        {
            var result = await _schedulerService.GetAll();
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<SchedulerEventResponseDto>>> GetById(int id)
        {
            var result = await _schedulerService.GetById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<SchedulerEventResponseDto>>> Create([FromBody] CreateSchedulerEventDto dto)
        {
            var result = await _schedulerService.Create(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<SchedulerEventResponseDto>>> Update(int id, [FromBody] UpdateSchedulerEventDto dto)
        {
            var result = await _schedulerService.Update(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _schedulerService.Delete(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}

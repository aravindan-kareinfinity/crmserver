using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SchedulerEventsController : ControllerBase
    {
        ISchedulerService schedulerService;

        public SchedulerEventsController(ISchedulerService schedulerService)
        {
            this.schedulerService = schedulerService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<SchedulerEventResponseDto>>>> GetAll()
        {
            var result = await schedulerService.GetAll();
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<SchedulerEventResponseDto>>> GetById(int id)
        {
            var result = await schedulerService.GetById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<SchedulerEventResponseDto>>> Create([FromBody] CreateSchedulerEventDto dto)
        {
            var result = await schedulerService.Create(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<SchedulerEventResponseDto>>> Update(int id, [FromBody] UpdateSchedulerEventDto dto)
        {
            var result = await schedulerService.Update(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await schedulerService.Delete(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}

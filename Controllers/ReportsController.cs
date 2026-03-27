using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<ReportResponseDto>>>> GetAll()
        {
            var result = await _reportService.GetAll();
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<ReportResponseDto>>> GetById(int id)
        {
            var result = await _reportService.GetById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ReportResponseDto>>> Create([FromBody] CreateReportDto dto)
        {
            var result = await _reportService.Create(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<ReportResponseDto>>> Update(int id, [FromBody] UpdateReportDto dto)
        {
            var result = await _reportService.Update(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await _reportService.Delete(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        /// <summary>Execute stored <see cref="ReportResponseDto.Query"/> with bound date parameters.</summary>
        [HttpPost("{id:int}/run")]
        public async Task<ActionResult<ApiResponse<ReportRunResultDto>>> Run(int id, [FromBody] RunReportRequestDto dto)
        {
            var result = await _reportService.Run(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}

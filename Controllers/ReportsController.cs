using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        IReportService reportService;

        public ReportsController(IReportService reportService)
        {
            this.reportService = reportService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<ReportResponseDto>>>> GetAll()
        {
            var result = await reportService.GetAll();
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<ReportResponseDto>>> GetById(int id)
        {
            var result = await reportService.GetById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ReportResponseDto>>> Create([FromBody] CreateReportDto dto)
        {
            var result = await reportService.Create(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<ReportResponseDto>>> Update(int id, [FromBody] UpdateReportDto dto)
        {
            var result = await reportService.Update(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
        {
            var result = await reportService.Delete(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        /// <summary>Execute stored <see cref="ReportResponseDto.Query"/> with bound date parameters.</summary>
        [HttpPost("{id:int}/run")]
        public async Task<ActionResult<ApiResponse<ReportRunResultDto>>> Run(int id, [FromBody] RunReportRequestDto dto)
        {
            var result = await reportService.Run(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}

using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvestmentsController : ControllerBase
    {
        IInvestmentService investmentService;

        public InvestmentsController(IInvestmentService investmentService)
        {
            this.investmentService = investmentService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<InvestmentResponseDto>>>> GetAll()
        {
            var result = await investmentService.GetAll();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>Must be registered before <c>customer/{customerId}</c> so "total" is not captured as customerId.</summary>
        [HttpGet("customer/{customerId}/total")]
        public async Task<ActionResult<ApiResponse<decimal>>> GetTotalInvestmentByCustomer(int customerId)
        {
            var result = await investmentService.GetTotalInvestmentByCustomer(customerId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code/total")]
        public async Task<ActionResult<ApiResponse<decimal>>> GetTotalInvestmentByCustomerCode([FromQuery] string customerCode)
        {
            var result = await investmentService.GetTotalInvestmentByCustomerCode(customerCode);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId}/list")]
        public async Task<ActionResult<ApiResponse<List<InvestmentResponseDto>>>> GetByCustomerIdList(int customerId)
        {
            var result = await investmentService.GetByCustomerId(customerId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code/list")]
        public async Task<ActionResult<ApiResponse<List<InvestmentResponseDto>>>> GetInvestmentsListByCustomerCode([FromQuery] string customerCode)
        {
            var result = await investmentService.GetByCustomerCode(customerCode);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<InvestmentResponseDto>>>> GetInvestmentsByCustomer(
            int customerId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await investmentService.GetInvestmentsByCustomer(customerId, pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<InvestmentResponseDto>>>> GetInvestmentsByCustomerCode(
            [FromQuery] string customerCode,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await investmentService.GetInvestmentsByCustomerCode(customerCode, pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("staff/{staffId}")]
        public async Task<ActionResult<ApiResponse<List<InvestmentResponseDto>>>> GetByStaffId(int staffId)
        {
            var result = await investmentService.GetByStaffId(staffId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<InvestmentResponseDto>>> GetInvestmentById(int id)
        {
            var result = await investmentService.GetInvestmentById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpGet("{id:int}/timeline")]
        public async Task<ActionResult<ApiResponse<List<InvestmentTimelineEntryDto>>>> GetTimeline(int id)
        {
            var result = await investmentService.GetTimeline(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{id:int}/timeline")]
        public async Task<ActionResult<ApiResponse<InvestmentTimelineEntryDto>>> AddTimelineEntry(int id, AddTimelineEntryDto dto)
        {
            var result = await investmentService.AddTimelineEntry(id, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{id:int}/claim")]
        public async Task<ActionResult<ApiResponse<InvestmentResponseDto>>> Claim(int id, ClaimInvestmentDto dto)
        {
            dto.InvestmentId = id;
            var result = await investmentService.ClaimInvestment(dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("claims/summary")]
        public async Task<ActionResult<ApiResponse<List<InvestmentClaimSummaryDto>>>> GetClaimSummary(
            [FromQuery] string startDate,
            [FromQuery] string endDate,
            [FromQuery] long? userId)
        {
            if (!DateTime.TryParse(startDate, out var start))
                return BadRequest(new ApiResponse<List<InvestmentClaimSummaryDto>> { Success = false, Message = "Invalid startDate" });
            if (!DateTime.TryParse(endDate, out var end))
                return BadRequest(new ApiResponse<List<InvestmentClaimSummaryDto>> { Success = false, Message = "Invalid endDate" });
            // include full end day
            var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc).AddDays(1).AddTicks(-1);

            var result = await investmentService.GetClaimSummary(startUtc, endUtc, userId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("claims/list")]
        public async Task<ActionResult<ApiResponse<List<InvestmentClaimRowDto>>>> GetClaimRows(
            [FromQuery] string startDate,
            [FromQuery] string endDate,
            [FromQuery] long? userId)
        {
            if (!DateTime.TryParse(startDate, out var start))
                return BadRequest(new ApiResponse<List<InvestmentClaimRowDto>> { Success = false, Message = "Invalid startDate" });
            if (!DateTime.TryParse(endDate, out var end))
                return BadRequest(new ApiResponse<List<InvestmentClaimRowDto>> { Success = false, Message = "Invalid endDate" });
            var startUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc).AddDays(1).AddTicks(-1);

            var result = await investmentService.GetClaimRows(startUtc, endUtc, userId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<InvestmentResponseDto>>> CreateInvestment(CreateInvestmentDto dto)
        {
            var result = await investmentService.CreateInvestment(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetInvestmentById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<InvestmentResponseDto>>> UpdateInvestment(int id, UpdateInvestmentDto dto)
        {
            var result = await investmentService.UpdateInvestment(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteInvestment(int id)
        {
            var result = await investmentService.DeleteInvestment(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}

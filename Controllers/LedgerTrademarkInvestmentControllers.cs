using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrademarksController : ControllerBase
    {
        private readonly ITrademarkService _trademarkService;

        public TrademarksController(ITrademarkService trademarkService)
        {
            _trademarkService = trademarkService;
        }

        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<List<TrademarkResponseDto>>>> GetAll()
        {
            var result = await _trademarkService.GetAll();
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<TrademarkResponseDto>>>> GetTrademarksByCustomer(
            int customerId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _trademarkService.GetTrademarksByCustomer(customerId, pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<TrademarkResponseDto>>>> GetTrademarksByCustomerCode(
            [FromQuery] string customerCode,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _trademarkService.GetTrademarksByCustomerCode(customerCode, pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("active/{isActive:bool}")]
        public async Task<ActionResult<ApiResponse<List<TrademarkResponseDto>>>> GetTrademarksByActive(bool isActive)
        {
            var result = await _trademarkService.GetTrademarksByActive(isActive);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<TrademarkResponseDto>>> GetTrademarkById(int id)
        {
            var result = await _trademarkService.GetTrademarkById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<TrademarkResponseDto>>> CreateTrademark(CreateTrademarkDto dto)
        {
            var result = await _trademarkService.CreateTrademark(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetTrademarkById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<TrademarkResponseDto>>> UpdateTrademark(int id, UpdateTrademarkDto dto)
        {
            var result = await _trademarkService.UpdateTrademark(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteTrademark(int id)
        {
            var result = await _trademarkService.DeleteTrademark(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class InvestmentsController : ControllerBase
    {
        private readonly IInvestmentService _investmentService;

        public InvestmentsController(IInvestmentService investmentService)
        {
            _investmentService = investmentService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<InvestmentResponseDto>>>> GetAll()
        {
            var result = await _investmentService.GetAll();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>Must be registered before <c>customer/{customerId}</c> so "total" is not captured as customerId.</summary>
        [HttpGet("customer/{customerId}/total")]
        public async Task<ActionResult<ApiResponse<decimal>>> GetTotalInvestmentByCustomer(int customerId)
        {
            var result = await _investmentService.GetTotalInvestmentByCustomer(customerId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code/total")]
        public async Task<ActionResult<ApiResponse<decimal>>> GetTotalInvestmentByCustomerCode([FromQuery] string customerCode)
        {
            var result = await _investmentService.GetTotalInvestmentByCustomerCode(customerCode);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId}/list")]
        public async Task<ActionResult<ApiResponse<List<InvestmentResponseDto>>>> GetByCustomerIdList(int customerId)
        {
            var result = await _investmentService.GetByCustomerId(customerId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code/list")]
        public async Task<ActionResult<ApiResponse<List<InvestmentResponseDto>>>> GetInvestmentsListByCustomerCode([FromQuery] string customerCode)
        {
            var result = await _investmentService.GetByCustomerCode(customerCode);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<InvestmentResponseDto>>>> GetInvestmentsByCustomer(
            int customerId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _investmentService.GetInvestmentsByCustomer(customerId, pageNumber, pageSize);
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
            var result = await _investmentService.GetInvestmentsByCustomerCode(customerCode, pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("staff/{staffId}")]
        public async Task<ActionResult<ApiResponse<List<InvestmentResponseDto>>>> GetByStaffId(int staffId)
        {
            var result = await _investmentService.GetByStaffId(staffId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<InvestmentResponseDto>>> GetInvestmentById(int id)
        {
            var result = await _investmentService.GetInvestmentById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpGet("{id:int}/timeline")]
        public async Task<ActionResult<ApiResponse<List<InvestmentTimelineEntryDto>>>> GetTimeline(int id)
        {
            var result = await _investmentService.GetTimeline(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{id:int}/timeline")]
        public async Task<ActionResult<ApiResponse<InvestmentTimelineEntryDto>>> AddTimelineEntry(int id, AddTimelineEntryDto dto)
        {
            var result = await _investmentService.AddTimelineEntry(id, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{id:int}/claim")]
        public async Task<ActionResult<ApiResponse<InvestmentResponseDto>>> Claim(int id, ClaimInvestmentDto dto)
        {
            dto.InvestmentId = id;
            var result = await _investmentService.ClaimInvestment(dto);
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

            var result = await _investmentService.GetClaimSummary(startUtc, endUtc, userId);
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

            var result = await _investmentService.GetClaimRows(startUtc, endUtc, userId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<InvestmentResponseDto>>> CreateInvestment(CreateInvestmentDto dto)
        {
            var result = await _investmentService.CreateInvestment(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetInvestmentById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<InvestmentResponseDto>>> UpdateInvestment(int id, UpdateInvestmentDto dto)
        {
            var result = await _investmentService.UpdateInvestment(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteInvestment(int id)
        {
            var result = await _investmentService.DeleteInvestment(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}

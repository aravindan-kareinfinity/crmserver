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
}

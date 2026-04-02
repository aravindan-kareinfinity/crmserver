using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrademarksController : ControllerBase
    {
        ITrademarkService trademarkService;

        public TrademarksController(ITrademarkService trademarkService)
        {
            this.trademarkService = trademarkService;
        }

        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<List<TrademarkResponseDto>>>> GetAll()
        {
            var result = await trademarkService.GetAll();
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
            var result = await trademarkService.GetTrademarksByCustomer(customerId, pageNumber, pageSize);
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
            var result = await trademarkService.GetTrademarksByCustomerCode(customerCode, pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("active/{isActive:bool}")]
        public async Task<ActionResult<ApiResponse<List<TrademarkResponseDto>>>> GetTrademarksByActive(bool isActive)
        {
            var result = await trademarkService.GetTrademarksByActive(isActive);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<TrademarkResponseDto>>> GetTrademarkById(int id)
        {
            var result = await trademarkService.GetTrademarkById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<TrademarkResponseDto>>> CreateTrademark(CreateTrademarkDto dto)
        {
            var result = await trademarkService.CreateTrademark(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetTrademarkById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<TrademarkResponseDto>>> UpdateTrademark(int id, UpdateTrademarkDto dto)
        {
            var result = await trademarkService.UpdateTrademark(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteTrademark(int id)
        {
            var result = await trademarkService.DeleteTrademark(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}

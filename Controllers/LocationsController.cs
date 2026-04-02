using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationsController : ControllerBase
    {
        private readonly ILocationService _locationService;

        public LocationsController(ILocationService locationService)
        {
            _locationService = locationService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<LocationResponseDto>>>> GetAll()
        {
            var result = await _locationService.GetAll();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<LocationResponseDto>>> GetById(int id)
        {
            var result = await _locationService.GetById(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId:int}/list")]
        public async Task<ActionResult<ApiResponse<List<LocationResponseDto>>>> GetByCustomerId(int customerId)
        {
            var result = await _locationService.GetByCustomerId(customerId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code/list")]
        public async Task<ActionResult<ApiResponse<List<LocationResponseDto>>>> GetLocationsListByCustomerCode([FromQuery] string customerCode)
        {
            var result = await _locationService.GetByCustomerCode(customerCode);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId:int}")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<LocationResponseDto>>>> GetLocationsByCustomer(
            int customerId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _locationService.GetLocationsByCustomer(customerId, pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<LocationResponseDto>>>> GetLocationsByCustomerCode(
            [FromQuery] string customerCode,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _locationService.GetLocationsByCustomerCode(customerCode, pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}/timeline")]
        public async Task<ActionResult<ApiResponse<List<LocationTimelineEntryDto>>>> GetTimeline(int id)
        {
            var result = await _locationService.GetTimeline(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<LocationResponseDto>>> CreateLocation(CreateLocationDto dto)
        {
            var result = await _locationService.CreateLocation(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<LocationResponseDto>>> UpdateLocation(int id, CreateLocationDto dto)
        {
            var result = await _locationService.UpdateLocation(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteLocation(int id)
        {
            var result = await _locationService.DeleteLocation(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}

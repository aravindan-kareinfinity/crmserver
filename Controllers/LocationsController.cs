using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationsController : ControllerBase
    {
        ILocationService locationService;

        public LocationsController(ILocationService locationService)
        {
            this.locationService = locationService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<LocationResponseDto>>>> GetAll()
        {
            var result = await locationService.GetAll();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<LocationResponseDto>>> GetById(int id)
        {
            var result = await locationService.GetById(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId:int}/list")]
        public async Task<ActionResult<ApiResponse<List<LocationResponseDto>>>> GetByCustomerId(int customerId)
        {
            var result = await locationService.GetByCustomerId(customerId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code/list")]
        public async Task<ActionResult<ApiResponse<List<LocationResponseDto>>>> GetLocationsListByCustomerCode([FromQuery] string customerCode)
        {
            var result = await locationService.GetByCustomerCode(customerCode);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId:int}")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<LocationResponseDto>>>> GetLocationsByCustomer(
            int customerId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await locationService.GetLocationsByCustomer(customerId, pageNumber, pageSize);
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
            var result = await locationService.GetLocationsByCustomerCode(customerCode, pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}/timeline")]
        public async Task<ActionResult<ApiResponse<List<LocationTimelineEntryDto>>>> GetTimeline(int id)
        {
            var result = await locationService.GetTimeline(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<LocationResponseDto>>> CreateLocation(CreateLocationDto dto)
        {
            var result = await locationService.CreateLocation(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<LocationResponseDto>>> UpdateLocation(int id, CreateLocationDto dto)
        {
            var result = await locationService.UpdateLocation(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteLocation(int id)
        {
            var result = await locationService.DeleteLocation(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}

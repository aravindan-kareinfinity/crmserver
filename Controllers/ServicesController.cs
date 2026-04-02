using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServicesController : ControllerBase
    {
        IServiceService serviceService;

        public ServicesController(IServiceService serviceService)
        {
            this.serviceService = serviceService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<ServiceResponseDto>>>> GetAllServices(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await serviceService.GetAllServices(pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<List<ServiceResponseDto>>>> GetAllServicesList()
        {
            var result = await serviceService.GetAllServicesList();
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("implementation-assignments")]
        public async Task<ActionResult<ApiResponse<List<ImplementationAssignmentDto>>>> GetAllImplementationAssignments()
        {
            var result = await serviceService.GetAllImplementationAssignments();
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<ServiceResponseDto>>> GetServiceById(int id)
        {
            var result = await serviceService.GetServiceById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId:int}")]
        public async Task<ActionResult<ApiResponse<List<ServiceResponseDto>>>> GetServicesByCustomer(int customerId)
        {
            var result = await serviceService.GetServicesByCustomer(customerId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code")]
        public async Task<ActionResult<ApiResponse<List<ServiceResponseDto>>>> GetServicesByCustomerCode([FromQuery] string customerCode)
        {
            var result = await serviceService.GetServicesByCustomerCode(customerCode);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}/implementation-timeline")]
        public async Task<ActionResult<ApiResponse<List<ImplementationTimelineEntryDto>>>> GetImplementationTimeline(int id)
        {
            var result = await serviceService.GetImplementationTimeline(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{id:int}/implementation-timeline")]
        public async Task<ActionResult<ApiResponse<ImplementationTimelineEntryDto>>> AddImplementationTimelineEntry(
            int id,
            [FromBody] AddImplementationTimelineEntryDto dto)
        {
            var result = await serviceService.AddImplementationTimelineEntry(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPut("{id:int}/implementation-assignment")]
        public async Task<ActionResult<ApiResponse<ImplementationAssignmentDto>>> UpsertImplementationAssignment(
            int id,
            [FromBody] UpsertImplementationAssignmentDto dto)
        {
            var result = await serviceService.UpsertImplementationAssignment(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ServiceResponseDto>>> CreateService(CreateServiceDto dto)
        {
            var result = await serviceService.CreateService(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetServiceById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<ServiceResponseDto>>> UpdateService(int id, UpdateServiceDto dto)
        {
            var result = await serviceService.UpdateService(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPut("{id:int}/go-live")]
        public async Task<ActionResult<ApiResponse<ServiceResponseDto>>> GoLive(int id, [FromBody] GoLiveServiceDto dto)
        {
            var result = await serviceService.GoLive(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteService(int id)
        {
            var result = await serviceService.DeleteService(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}

using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        ICustomerService customerService;

        public CustomersController(ICustomerService customerService)
        {
            this.customerService = customerService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<CustomerResponseDto>>>> GetAllCustomers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null)
        {
            var result = await customerService.GetAllCustomers(pageNumber, pageSize, searchTerm);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        /// <summary>UI <c>customerService.getAll()</c> — full list, no pagination.</summary>
        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<List<CustomerResponseDto>>>> GetAllList()
        {
            var result = await customerService.GetAllCustomersList();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<CustomerResponseDto>>> GetCustomerById(int id)
        {
            var result = await customerService.GetCustomerById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        /// <summary>Lookup by stable <c>customers.code</c> (e.g. 2026/0001).</summary>
        [HttpGet("by-code")]
        public async Task<ActionResult<ApiResponse<CustomerResponseDto>>> GetCustomerByCode([FromQuery] string code)
        {
            var result = await customerService.GetCustomerByCode(code);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpGet("type-id/{typeId:int}")]
        public async Task<ActionResult<ApiResponse<List<CustomerResponseDto>>>> GetCustomersByTypeId(int typeId)
        {
            var result = await customerService.GetCustomersByTypeId(typeId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        /// <summary>UI <c>getByType('lead' | 'prospect' | 'customer')</c>.</summary>
        [HttpGet("type/{type}")]
        public async Task<ActionResult<ApiResponse<List<CustomerResponseDto>>>> GetCustomersByType(string type)
        {
            var result = await customerService.GetCustomersByType(type);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        /// <summary>
        /// UI Contacts (/contacts): leads + prospects only (excludes customers).
        /// </summary>
        [HttpGet("contacts")]
        public async Task<ActionResult<ApiResponse<List<CustomerResponseDto>>>> GetContacts()
        {
            var lead = await customerService.GetCustomersByType("lead");
            if (!lead.Success) return BadRequest(lead);
            var prospect = await customerService.GetCustomersByType("prospect");
            if (!prospect.Success) return BadRequest(prospect);

            var merged = lead.Data.Concat(prospect.Data)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return Ok(new ApiResponse<List<CustomerResponseDto>> { Success = true, Data = merged });
        }

        [HttpGet("{customerId:int}/timeline")]
        public async Task<ActionResult<ApiResponse<List<CustomerTimelineEntryDto>>>> GetCustomerTimeline(int customerId)
        {
            var result = await customerService.GetCustomerTimeline(customerId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("timeline/by-code")]
        public async Task<ActionResult<ApiResponse<List<CustomerTimelineEntryDto>>>> GetCustomerTimelineByCode([FromQuery] string customerCode)
        {
            var result = await customerService.GetCustomerTimelineByCustomerCode(customerCode);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{customerId:int}/timeline")]
        public async Task<ActionResult<ApiResponse<CustomerTimelineEntryDto>>> AddCustomerTimelineEntry(
            int customerId,
            [FromBody] AddTimelineEntryDto dto)
        {
            var result = await customerService.AddCustomerTimelineEntry(customerId, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("timeline/by-code")]
        public async Task<ActionResult<ApiResponse<CustomerTimelineEntryDto>>> AddCustomerTimelineByCode(
            [FromQuery] string customerCode,
            [FromBody] AddTimelineEntryDto dto)
        {
            var result = await customerService.AddCustomerTimelineEntryByCustomerCode(customerCode, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>Multipart .xlsx bulk import; must be registered before parameterized POST routes.</summary>
        [HttpPost("bulk")]
        [RequestSizeLimit(20_000_000)]
        public async Task<ActionResult<ApiResponse<BulkImportCustomersResultDto>>> BulkImportCustomers(
            IFormFile? file,
            [FromForm] long? userId = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new ApiResponse<BulkImportCustomersResultDto>
                {
                    Success = false,
                    Message = "No file uploaded",
                    Data = new BulkImportCustomersResultDto()
                });
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".xlsx")
                return BadRequest(new ApiResponse<BulkImportCustomersResultDto>
                {
                    Success = false,
                    Message = "Only .xlsx is supported (save as Excel workbook, not .xls)",
                    Data = new BulkImportCustomersResultDto()
                });

            await using var stream = file.OpenReadStream();
            var result = await customerService.ImportCustomersFromSpreadsheetAsync(stream, userId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<CustomerResponseDto>>> CreateCustomer(CreateCustomerDto dto)
        {
            var result = await customerService.CreateCustomer(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetCustomerById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<CustomerResponseDto>>> UpdateCustomer(int id, UpdateCustomerDto dto)
        {
            var result = await customerService.UpdateCustomer(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteCustomer(int id)
        {
            var result = await customerService.DeleteCustomer(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}

using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoicesController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoicesController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<InvoiceResponseDto>>>> GetAllInvoices(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _invoiceService.GetAllInvoices(pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<List<InvoiceResponseDto>>>> GetAll()
        {
            var result = await _invoiceService.GetAll();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<InvoiceResponseDto>>> GetById(int id)
        {
            var result = await _invoiceService.GetById(id);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId:int}")]
        public async Task<ActionResult<ApiResponse<List<InvoiceResponseDto>>>> GetInvoicesByCustomer(int customerId)
        {
            var result = await _invoiceService.GetInvoicesByCustomer(customerId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/by-code")]
        public async Task<ActionResult<ApiResponse<List<InvoiceResponseDto>>>> GetInvoicesByCustomerCode([FromQuery] string customerCode)
        {
            var result = await _invoiceService.GetInvoicesByCustomerCode(customerCode);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("staff/{staffId:int}")]
        public async Task<ActionResult<ApiResponse<List<InvoiceResponseDto>>>> GetByStaffId(int staffId)
        {
            var result = await _invoiceService.GetByStaffId(staffId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}/timeline")]
        public async Task<ActionResult<ApiResponse<List<InvoiceTimelineEntryDto>>>> GetTimeline(int id)
        {
            var result = await _invoiceService.GetTimeline(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{id:int}/timeline")]
        public async Task<ActionResult<ApiResponse<InvoiceTimelineEntryDto>>> AddTimelineEntry(int id, AddTimelineEntryDto dto)
        {
            var result = await _invoiceService.AddTimelineEntry(id, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<InvoiceResponseDto>>> CreateInvoice(CreateInvoiceDto dto)
        {
            var result = await _invoiceService.CreateInvoice(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<InvoiceResponseDto>>> UpdateInvoice(int id, UpdateInvoiceDto dto)
        {
            var result = await _invoiceService.UpdateInvoice(id, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteInvoice(int id)
        {
            var result = await _invoiceService.DeleteInvoice(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}

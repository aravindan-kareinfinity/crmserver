using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace CRM.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomersController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<CustomerResponseDto>>>> GetAllCustomers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? searchTerm = null)
        {
            var result = await _customerService.GetAllCustomers(pageNumber, pageSize, searchTerm);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        /// <summary>UI <c>customerService.getAll()</c> — full list, no pagination.</summary>
        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<List<CustomerResponseDto>>>> GetAllList()
        {
            var result = await _customerService.GetAllCustomersList();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<CustomerResponseDto>>> GetCustomerById(int id)
        {
            var result = await _customerService.GetCustomerById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpGet("type-id/{typeId:int}")]
        public async Task<ActionResult<ApiResponse<List<CustomerResponseDto>>>> GetCustomersByTypeId(int typeId)
        {
            var result = await _customerService.GetCustomersByTypeId(typeId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        /// <summary>UI <c>getByType('lead' | 'prospect' | 'customer')</c>.</summary>
        [HttpGet("type/{type}")]
        public async Task<ActionResult<ApiResponse<List<CustomerResponseDto>>>> GetCustomersByType(string type)
        {
            var result = await _customerService.GetCustomersByType(type);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{customerId:int}/timeline")]
        public async Task<ActionResult<ApiResponse<List<CustomerTimelineEntryDto>>>> GetCustomerTimeline(int customerId)
        {
            var result = await _customerService.GetCustomerTimeline(customerId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{customerId:int}/timeline")]
        public async Task<ActionResult<ApiResponse<CustomerTimelineEntryDto>>> AddCustomerTimelineEntry(
            int customerId,
            [FromBody] AddTimelineEntryDto dto)
        {
            var result = await _customerService.AddCustomerTimelineEntry(customerId, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        /// <summary>Multipart .xlsx bulk import; must be registered before parameterized POST routes.</summary>
        [HttpPost("bulk")]
        [RequestSizeLimit(20_000_000)]
        public async Task<ActionResult<ApiResponse<BulkImportCustomersResultDto>>> BulkImportCustomers(IFormFile? file)
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
            var result = await _customerService.ImportCustomersFromSpreadsheetAsync(stream);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<CustomerResponseDto>>> CreateCustomer(CreateCustomerDto dto)
        {
            var result = await _customerService.CreateCustomer(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetCustomerById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<CustomerResponseDto>>> UpdateCustomer(int id, UpdateCustomerDto dto)
        {
            var result = await _customerService.UpdateCustomer(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteCustomer(int id)
        {
            var result = await _customerService.DeleteCustomer(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ServicesController : ControllerBase
    {
        private readonly IServiceService _serviceService;

        public ServicesController(IServiceService serviceService)
        {
            _serviceService = serviceService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<ServiceResponseDto>>>> GetAllServices(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _serviceService.GetAllServices(pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<List<ServiceResponseDto>>>> GetAllServicesList()
        {
            var result = await _serviceService.GetAllServicesList();
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("implementation-assignments")]
        public async Task<ActionResult<ApiResponse<List<ImplementationAssignmentDto>>>> GetAllImplementationAssignments()
        {
            var result = await _serviceService.GetAllImplementationAssignments();
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<ServiceResponseDto>>> GetServiceById(int id)
        {
            var result = await _serviceService.GetServiceById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId:int}")]
        public async Task<ActionResult<ApiResponse<List<ServiceResponseDto>>>> GetServicesByCustomer(int customerId)
        {
            var result = await _serviceService.GetServicesByCustomer(customerId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}/implementation-timeline")]
        public async Task<ActionResult<ApiResponse<List<ImplementationTimelineEntryDto>>>> GetImplementationTimeline(int id)
        {
            var result = await _serviceService.GetImplementationTimeline(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{id:int}/implementation-timeline")]
        public async Task<ActionResult<ApiResponse<ImplementationTimelineEntryDto>>> AddImplementationTimelineEntry(
            int id,
            [FromBody] AddImplementationTimelineEntryDto dto)
        {
            var result = await _serviceService.AddImplementationTimelineEntry(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPut("{id:int}/implementation-assignment")]
        public async Task<ActionResult<ApiResponse<ImplementationAssignmentDto>>> UpsertImplementationAssignment(
            int id,
            [FromBody] UpsertImplementationAssignmentDto dto)
        {
            var result = await _serviceService.UpsertImplementationAssignment(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ServiceResponseDto>>> CreateService(CreateServiceDto dto)
        {
            var result = await _serviceService.CreateService(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetServiceById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<ServiceResponseDto>>> UpdateService(int id, UpdateServiceDto dto)
        {
            var result = await _serviceService.UpdateService(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteService(int id)
        {
            var result = await _serviceService.DeleteService(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }

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

    [ApiController]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly ITicketService _ticketService;

        public TicketsController(ITicketService ticketService)
        {
            _ticketService = ticketService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<TicketResponseDto>>>> GetAllTickets(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _ticketService.GetAllTickets(pageNumber, pageSize);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<ActionResult<ApiResponse<List<TicketResponseDto>>>> GetAll()
        {
            var result = await _ticketService.GetAll();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("customer/{customerId:int}")]
        public async Task<ActionResult<ApiResponse<List<TicketResponseDto>>>> GetByCustomerId(int customerId)
        {
            var result = await _ticketService.GetByCustomerId(customerId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("status/{status}")]
        public async Task<ActionResult<ApiResponse<List<TicketResponseDto>>>> GetByStatus(string status)
        {
            var result = await _ticketService.GetByStatus(status);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("assigned/{userId:int}")]
        public async Task<ActionResult<ApiResponse<List<TicketResponseDto>>>> GetByAssignedTo(int userId)
        {
            var result = await _ticketService.GetByAssignedTo(userId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}/timeline")]
        public async Task<ActionResult<ApiResponse<List<TicketTimelineEntryDto>>>> GetTimeline(int id)
        {
            var result = await _ticketService.GetTimeline(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{id:int}/timeline")]
        public async Task<ActionResult<ApiResponse<TicketTimelineEntryDto>>> AddTimelineEntry(int id, AddTicketTimelineEntryDto dto)
        {
            var result = await _ticketService.AddTimelineEntry(id, dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<TicketResponseDto>>> GetTicketById(int id)
        {
            var result = await _ticketService.GetTicketById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<TicketResponseDto>>> CreateTicket(CreateTicketDto dto)
        {
            var result = await _ticketService.CreateTicket(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetTicketById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<TicketResponseDto>>> UpdateTicket(int id, UpdateTicketDto dto)
        {
            var result = await _ticketService.UpdateTicket(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteTicket(int id)
        {
            var result = await _ticketService.DeleteTicket(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }

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

    [ApiController]
    [Route("api/[controller]")]
    public class ReferencesController : ControllerBase
    {
        private readonly IReferenceService _referenceService;
        private readonly IPincodeGeoService _pincodeGeoService;

        public ReferencesController(IReferenceService referenceService, IPincodeGeoService pincodeGeoService)
        {
            _referenceService = referenceService;
            _pincodeGeoService = pincodeGeoService;
        }

        /// <summary>India Post pincode API → Country / State / City reference ids (creates reference rows when missing).</summary>
        [HttpGet("resolve-pincode/{pincode}")]
        public async Task<ActionResult<ApiResponse<PincodeResolveResponseDto>>> ResolvePincode(string pincode)
        {
            var result = await _pincodeGeoService.ResolveAsync(pincode);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<ReferenceResponseDto>>>> GetAll()
        {
            var result = await _referenceService.GetAll();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("category/{category}")]
        public async Task<ActionResult<ApiResponse<List<ReferenceResponseDto>>>> GetReferencesByCategory(string category)
        {
            var result = await _referenceService.GetReferencesByCategory(category);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("value/{value}")]
        public async Task<ActionResult<ApiResponse<ReferenceResponseDto>>> GetByValue(string value)
        {
            var result = await _referenceService.GetByValue(value);
            if (!result.Success) return NotFound(result);
            return Ok(result);
        }

        [HttpGet("{id:int}/label")]
        public async Task<ActionResult<ApiResponse<ReferenceLabelResponseDto>>> GetLabelById(int id)
        {
            var result = await _referenceService.GetLabelById(id);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("label-by-value/{value}")]
        public async Task<ActionResult<ApiResponse<ReferenceLabelResponseDto>>> GetLabelByValue(string value)
        {
            var result = await _referenceService.GetLabelByValue(value);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ApiResponse<ReferenceResponseDto>>> GetReferenceById(int id)
        {
            var result = await _referenceService.GetReferenceById(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ReferenceResponseDto>>> CreateReference([FromBody] CreateReferenceDto dto)
        {
            var result = await _referenceService.Create(dto);
            if (!result.Success)
                return BadRequest(result);
            return CreatedAtAction(nameof(GetReferenceById), new { id = result.Data?.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<ApiResponse<ReferenceResponseDto>>> UpdateReference(int id, [FromBody] UpdateReferenceDto dto)
        {
            var result = await _referenceService.Update(id, dto);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteReference(int id)
        {
            var result = await _referenceService.Delete(id);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}

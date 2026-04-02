using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
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

        [HttpGet("customer/by-code")]
        public async Task<ActionResult<ApiResponse<List<TicketResponseDto>>>> GetTicketsByCustomerCode([FromQuery] string customerCode)
        {
            var result = await _ticketService.GetByCustomerCode(customerCode);
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
}

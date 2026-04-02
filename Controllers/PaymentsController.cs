using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpGet("invoice/{invoiceId:int}")]
        public async Task<ActionResult<ApiResponse<List<PaymentResponseDto>>>> GetByInvoice(int invoiceId)
        {
            var result = await _paymentService.GetByInvoice(invoiceId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("collect")]
        public async Task<ActionResult<ApiResponse<CollectPaymentResultDto>>> Collect(CollectPaymentDto dto)
        {
            var result = await _paymentService.Collect(dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}

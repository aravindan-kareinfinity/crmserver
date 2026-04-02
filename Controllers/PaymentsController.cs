using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        IPaymentService paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            this.paymentService = paymentService;
        }

        [HttpGet("invoice/{invoiceId:int}")]
        public async Task<ActionResult<ApiResponse<List<PaymentResponseDto>>>> GetByInvoice(int invoiceId)
        {
            var result = await paymentService.GetByInvoice(invoiceId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("collect")]
        public async Task<ActionResult<ApiResponse<CollectPaymentResultDto>>> Collect(CollectPaymentDto dto)
        {
            var result = await paymentService.Collect(dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}

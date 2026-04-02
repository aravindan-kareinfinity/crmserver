using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
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

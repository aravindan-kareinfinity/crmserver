using CRM.Server.DTOs;
using CRM.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly IFileService _fileService;

        public FilesController(IFileService fileService)
        {
            _fileService = fileService;
        }

        /// <summary>Upload an image or any binary file; returns <see cref="FileStoredResponseDto.ImageId"/>.</summary>
        [HttpPost("upload")]
        [RequestSizeLimit(52_428_800)] // 50 MB
        public async Task<ActionResult<ApiResponse<FileStoredResponseDto>>> Upload(
            [FromForm] IFormFile file,
            [FromForm] long? createdBy = null,
            [FromForm] string? notes = null,
            [FromForm] long? parentId = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new ApiResponse<FileStoredResponseDto> { Success = false, Message = "No file uploaded" });

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var result = await _fileService.StoreAsync(bytes, file.ContentType, createdBy, notes, parentId);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        /// <summary>Download raw bytes with Content-Type from <c>files.type</c>.</summary>
        [HttpGet("{id:long}/content")]
        public async Task<IActionResult> GetContent(long id)
        {
            var result = await _fileService.GetContentAsync(id);
            if (!result.Success)
                return NotFound(result);
            return File(result.Data.Content, result.Data.ContentType);
        }

        [HttpGet("{id:long}/metadata")]
        public async Task<ActionResult<ApiResponse<FileMetadataResponseDto>>> GetMetadata(long id)
        {
            var result = await _fileService.GetMetadataAsync(id);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }
    }
}

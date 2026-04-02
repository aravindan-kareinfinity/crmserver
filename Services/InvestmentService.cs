using CRM.Server.Data;
using CRM.Server.DTOs;
using CRM.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>Parity with core-crm-suite <c>InvestmentService.ts</c>.</summary>
    public interface IInvestmentService
    {
        Task<ApiResponse<List<InvestmentResponseDto>>> GetAll();
        Task<ApiResponse<InvestmentResponseDto>> GetInvestmentById(int id);
        Task<ApiResponse<PaginatedResponse<InvestmentResponseDto>>> GetInvestmentsByCustomer(int customerId, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<List<InvestmentResponseDto>>> GetByCustomerId(int customerId);
        Task<ApiResponse<List<InvestmentResponseDto>>> GetByCustomerCode(string customerCode);
        Task<ApiResponse<PaginatedResponse<InvestmentResponseDto>>> GetInvestmentsByCustomerCode(string customerCode, int pageNumber = 1, int pageSize = 10);
        Task<ApiResponse<decimal>> GetTotalInvestmentByCustomerCode(string customerCode);
        Task<ApiResponse<List<InvestmentResponseDto>>> GetByStaffId(int staffId);
        Task<ApiResponse<InvestmentResponseDto>> CreateInvestment(CreateInvestmentDto dto);
        Task<ApiResponse<InvestmentResponseDto>> UpdateInvestment(int id, UpdateInvestmentDto dto);
        Task<ApiResponse<bool>> DeleteInvestment(int id);
        Task<ApiResponse<decimal>> GetTotalInvestmentByCustomer(int customerId);
        Task<ApiResponse<List<InvestmentTimelineEntryDto>>> GetTimeline(int investmentId);
        Task<ApiResponse<InvestmentTimelineEntryDto>> AddTimelineEntry(int investmentId, AddTimelineEntryDto dto);
        Task<ApiResponse<InvestmentResponseDto>> ClaimInvestment(ClaimInvestmentDto dto);
        Task<ApiResponse<List<InvestmentClaimSummaryDto>>> GetClaimSummary(DateTime startUtc, DateTime endUtc, long? userId);
        Task<ApiResponse<List<InvestmentClaimRowDto>>> GetClaimRows(DateTime startUtc, DateTime endUtc, long? userId);
    }

    public class InvestmentService : IInvestmentService
    {
        CrmDbContext context;

        public InvestmentService(CrmDbContext context)
        {
            this.context = context;
        }

        private static async Task EnrichInvestmentsAsync(CrmDbContext ctx, List<InvestmentResponseDto> rows) =>
            await EntityCodeResolution.EnrichInvestmentDtosAsync(ctx, rows);

        private static InvestmentResponseDto Map(Investment i) => new()
        {
            Id = i.Id,
            CustomerId = i.Customer?.Id ?? 0,
            LocationId = i.LocationId,
            Amount = i.Amount,
            ClaimedAmount = i.ClaimedAmount,
            RemainingAmount = i.RemainingAmount,
            ClaimedFully = i.ClaimedFully,
            ClaimedAt = i.ClaimedAt,
            ClaimedBy = i.ClaimedBy,
            ClaimNotes = i.ClaimNotes,
            NeedsClaim = i.NeedsClaim,
            InvestmentTypeId = i.InvestmentTypeId,
            StaffId = i.StaffId,
            Notes = i.Notes,
            IsActive = i.IsActive,
            CreatedAt = i.CreatedAt,
            CreatedBy = i.CreatedBy,
            ModifiedAt = i.ModifiedAt
        };

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetAll()
        {
            try
            {
                var list = await context.Investments.Include(i => i.Customer).OrderByDescending(i => i.CreatedAt).ToListAsync();
                var dtos = list.Select(Map).ToList();
                await EnrichInvestmentsAsync(context, dtos);
                return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvestmentResponseDto>> GetInvestmentById(int id)
        {
            try
            {
                var i = await context.Investments.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id);
                if (i == null) return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "Investment not found" };
                var one = Map(i);
                await EnrichInvestmentsAsync(context, new List<InvestmentResponseDto> { one });
                return new ApiResponse<InvestmentResponseDto> { Success = true, Data = one };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvestmentResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<InvestmentResponseDto>>> GetInvestmentsByCustomer(int customerId, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var cc = await EntityCodeResolution.GetCustomerCodeByIdAsync(context, customerId);
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<PaginatedResponse<InvestmentResponseDto>>
                    {
                        Success = true,
                        Data = new PaginatedResponse<InvestmentResponseDto>
                        {
                            Items = new List<InvestmentResponseDto>(),
                            Total = 0,
                            PageNumber = pageNumber,
                            PageSize = pageSize
                        }
                    };
                var q = context.Investments.Include(i => i.Customer).Where(i => i.CustomerCode == cc);
                var total = await q.CountAsync();
                var items = await q.OrderByDescending(i => i.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
                var dtos = items.Select(Map).ToList();
                await EnrichInvestmentsAsync(context, dtos);
                return new ApiResponse<PaginatedResponse<InvestmentResponseDto>>
                {
                    Success = true,
                    Data = new PaginatedResponse<InvestmentResponseDto>
                    {
                        Items = dtos,
                        Total = total,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetByCustomerId(int customerId)
        {
            try
            {
                var cc = await EntityCodeResolution.GetCustomerCodeByIdAsync(context, customerId);
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = new List<InvestmentResponseDto>() };
                var list = await context.Investments.Include(i => i.Customer).Where(i => i.CustomerCode == cc).OrderByDescending(i => i.CreatedAt).ToListAsync();
                var dtos = list.Select(Map).ToList();
                await EnrichInvestmentsAsync(context, dtos);
                return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetByCustomerCode(string customerCode)
        {
            try
            {
                var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(context, 0, customerCode);
                if (err != null)
                    return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = err };
                return await GetByCustomerId(cid);
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<PaginatedResponse<InvestmentResponseDto>>> GetInvestmentsByCustomerCode(
            string customerCode,
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(context, 0, customerCode);
                if (err != null)
                    return new ApiResponse<PaginatedResponse<InvestmentResponseDto>> { Success = false, Message = err };
                return await GetInvestmentsByCustomer(cid, pageNumber, pageSize);
            }
            catch (Exception ex)
            {
                return new ApiResponse<PaginatedResponse<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentResponseDto>>> GetByStaffId(int staffId)
        {
            try
            {
                var list = await context.Investments.Include(i => i.Customer).Where(i => i.StaffId == staffId).OrderByDescending(i => i.CreatedAt).ToListAsync();
                var dtos = list.Select(Map).ToList();
                await EnrichInvestmentsAsync(context, dtos);
                return new ApiResponse<List<InvestmentResponseDto>> { Success = true, Data = dtos };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentResponseDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvestmentResponseDto>> CreateInvestment(CreateInvestmentDto dto)
        {
            try
            {
                var (custCode, _, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(context, dto.CustomerId, dto.CustomerCode);
                if (cErr != null)
                    return new ApiResponse<InvestmentResponseDto> { Success = false, Message = cErr };
                var (locationId, lErr) = await EntityCodeResolution.ResolveRequiredLocationIdAsync(
                    context, custCode, dto.LocationId, dto.LocationCode);
                if (lErr != null)
                    return new ApiResponse<InvestmentResponseDto> { Success = false, Message = lErr };

                var now = DateTime.UtcNow;
                var inv = new Investment
                {
                    CustomerCode = custCode,
                    LocationId = locationId,
                    Amount = dto.Amount,
                    ClaimedAmount = 0,
                    RemainingAmount = dto.Amount < 0 ? 0 : dto.Amount,
                    ClaimedFully = false,
                    NeedsClaim = dto.NeedsClaim != false,
                    InvestmentTypeId = dto.InvestmentTypeId,
                    StaffId = dto.StaffId,
                    Notes = dto.Notes,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                context.Investments.Add(inv);
                await context.SaveChangesAsync();
                await context.Entry(inv).Reference(x => x.Customer).LoadAsync();
                var created = Map(inv);
                await EnrichInvestmentsAsync(context, new List<InvestmentResponseDto> { created });
                return new ApiResponse<InvestmentResponseDto> { Success = true, Data = created };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvestmentResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvestmentResponseDto>> UpdateInvestment(int id, UpdateInvestmentDto dto)
        {
            try
            {
                var i = await context.Investments.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id);
                if (i == null) return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "Investment not found" };

                if (!string.IsNullOrWhiteSpace(dto.CustomerCode))
                {
                    var (cc, _, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(context, 0, dto.CustomerCode);
                    if (cErr != null)
                        return new ApiResponse<InvestmentResponseDto> { Success = false, Message = cErr };
                    i.CustomerCode = cc;
                }
                else if (dto.CustomerId.HasValue)
                {
                    var (cc, _, cErr) = await EntityCodeResolution.ResolveCustomerLinkAsync(context, dto.CustomerId.Value, null);
                    if (cErr != null)
                        return new ApiResponse<InvestmentResponseDto> { Success = false, Message = cErr };
                    i.CustomerCode = cc;
                }

                if (!string.IsNullOrWhiteSpace(dto.LocationCode))
                {
                    var (lid, lErr) = await EntityCodeResolution.ResolveRequiredLocationIdAsync(
                        context, i.CustomerCode, 0, dto.LocationCode);
                    if (lErr != null)
                        return new ApiResponse<InvestmentResponseDto> { Success = false, Message = lErr };
                    i.LocationId = lid;
                }
                else if (dto.LocationId.HasValue)
                    i.LocationId = dto.LocationId.Value;
                if (dto.InvestmentTypeId.HasValue) i.InvestmentTypeId = dto.InvestmentTypeId.Value;
                if (dto.Amount.HasValue)
                {
                    i.Amount = dto.Amount.Value;
                    // keep claimed amounts consistent
                    if (i.ClaimedAmount < 0) i.ClaimedAmount = 0;
                    if (i.ClaimedAmount > i.Amount) i.ClaimedAmount = i.Amount;
                    i.RemainingAmount = Math.Max(0, i.Amount - i.ClaimedAmount);
                    i.ClaimedFully = i.RemainingAmount == 0 && i.Amount > 0;
                }
                if (dto.StaffIdCleared == true)
                    i.StaffId = null;
                else if (dto.StaffId.HasValue)
                    i.StaffId = dto.StaffId.Value;
                if (dto.Notes != null) i.Notes = dto.Notes;
                if (dto.IsActive.HasValue) i.IsActive = dto.IsActive.Value;
                if (dto.NeedsClaim.HasValue) i.NeedsClaim = dto.NeedsClaim.Value;
                i.ModifiedAt = DateTime.UtcNow;
                i.ModifiedBy = AuditUserIds.System;
                await context.SaveChangesAsync();
                await context.Entry(i).Reference(x => x.Customer).LoadAsync();
                var updated = Map(i);
                await EnrichInvestmentsAsync(context, new List<InvestmentResponseDto> { updated });
                return new ApiResponse<InvestmentResponseDto> { Success = true, Data = updated };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvestmentResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<bool>> DeleteInvestment(int id)
        {
            try
            {
                var i = await context.Investments.FindAsync(id);
                if (i == null) return new ApiResponse<bool> { Success = false, Message = "Investment not found" };
                context.Investments.Remove(i);
                await context.SaveChangesAsync();
                return new ApiResponse<bool> { Success = true, Data = true };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<decimal>> GetTotalInvestmentByCustomer(int customerId)
        {
            try
            {
                var cc = await EntityCodeResolution.GetCustomerCodeByIdAsync(context, customerId);
                if (string.IsNullOrEmpty(cc))
                    return new ApiResponse<decimal> { Success = true, Data = 0 };
                var total = await context.Investments.Where(i => i.CustomerCode == cc).SumAsync(i => i.Amount);
                return new ApiResponse<decimal> { Success = true, Data = total };
            }
            catch (Exception ex)
            {
                return new ApiResponse<decimal> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<decimal>> GetTotalInvestmentByCustomerCode(string customerCode)
        {
            try
            {
                var (cid, err) = await EntityCodeResolution.ResolveCustomerIdAsync(context, 0, customerCode);
                if (err != null)
                    return new ApiResponse<decimal> { Success = false, Message = err };
                return await GetTotalInvestmentByCustomer(cid);
            }
            catch (Exception ex)
            {
                return new ApiResponse<decimal> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentTimelineEntryDto>>> GetTimeline(int investmentId)
        {
            try
            {
                var rows = await context.InvestmentTimelines.Where(t => t.InvestmentId == investmentId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new InvestmentTimelineEntryDto
                    {
                        Id = t.Id,
                        InvestmentId = t.InvestmentId,
                        Type = t.Type,
                        Notes = t.Notes,
                        FileId = t.FileId,
                        FileName = t.FileName,
                        IsActive = t.IsActive,
                        CreatedAt = t.CreatedAt,
                        CreatedBy = t.CreatedBy,
                        ModifiedAt = t.ModifiedAt,
                        ModifiedBy = t.ModifiedBy
                    }).ToListAsync();
                return new ApiResponse<List<InvestmentTimelineEntryDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentTimelineEntryDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvestmentTimelineEntryDto>> AddTimelineEntry(int investmentId, AddTimelineEntryDto dto)
        {
            try
            {
                var parent = await context.Investments.FindAsync(investmentId);
                if (parent == null) return new ApiResponse<InvestmentTimelineEntryDto> { Success = false, Message = "Investment not found" };
                var now = DateTime.UtcNow;
                var e = new InvestmentTimeline
                {
                    InvestmentId = investmentId,
                    Type = dto.Type,
                    Notes = dto.Notes,
                    FileId = dto.FileId,
                    FileName = dto.FileName,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = AuditUserIds.System,
                    ModifiedAt = now,
                    ModifiedBy = AuditUserIds.System
                };
                context.InvestmentTimelines.Add(e);
                await context.SaveChangesAsync();
                return new ApiResponse<InvestmentTimelineEntryDto>
                {
                    Success = true,
                    Data = new InvestmentTimelineEntryDto
                    {
                        Id = e.Id,
                        InvestmentId = e.InvestmentId,
                        Type = e.Type,
                        Notes = e.Notes,
                        FileId = e.FileId,
                        FileName = e.FileName,
                        IsActive = e.IsActive,
                        CreatedAt = e.CreatedAt,
                        CreatedBy = e.CreatedBy,
                        ModifiedAt = e.ModifiedAt,
                        ModifiedBy = e.ModifiedBy
                    }
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<InvestmentTimelineEntryDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<InvestmentResponseDto>> ClaimInvestment(ClaimInvestmentDto dto)
        {
            if (dto.InvestmentId <= 0)
                return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "investmentId is required" };

            await using var tx = await context.Database.BeginTransactionAsync();
            try
            {
                var inv = await context.Investments.Include(i => i.Customer).FirstOrDefaultAsync(i => i.Id == dto.InvestmentId);
                if (inv == null)
                    return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "Investment not found" };
                if (inv.ClaimedFully)
                    return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "Already claimed" };
                if (!inv.NeedsClaim)
                    return new ApiResponse<InvestmentResponseDto> { Success = false, Message = "This investment does not require a claim" };

                var now = DateTime.UtcNow;
                var auditUserId = dto.UserId.HasValue && dto.UserId.Value > 0 ? dto.UserId.Value : AuditUserIds.System;
                var claimedAt = dto.ClaimedAt ?? now;

                var amount = inv.Amount < 0 ? 0 : inv.Amount;
                // Always full claim: close the investment in one step.
                var remaining = 0m;
                var claimAmount = Math.Max(0, amount);

                inv.ClaimedAmount = claimAmount;
                inv.RemainingAmount = remaining;
                inv.ClaimedFully = amount > 0;
                inv.ClaimedAt = claimedAt;
                inv.ClaimedBy = auditUserId;
                inv.ClaimNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
                inv.ModifiedAt = now;
                inv.ModifiedBy = auditUserId;

                context.InvestmentTimelines.Add(new InvestmentTimeline
                {
                    InvestmentId = inv.Id,
                    Type = 1,
                    Notes = $"Claimed fully: {claimAmount}",
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = auditUserId,
                    ModifiedAt = now,
                    ModifiedBy = auditUserId
                });

                await context.SaveChangesAsync();
                await tx.CommitAsync();

                var updated = Map(inv);
                await EnrichInvestmentsAsync(context, new List<InvestmentResponseDto> { updated });
                return new ApiResponse<InvestmentResponseDto> { Success = true, Data = updated };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return new ApiResponse<InvestmentResponseDto> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentClaimSummaryDto>>> GetClaimSummary(DateTime startUtc, DateTime endUtc, long? userId)
        {
            try
            {
                var q = context.Investments.AsNoTracking()
                    .Where(i => i.ClaimedFully && i.ClaimedAt != null);

                q = q.Where(i => i.ClaimedAt >= startUtc && i.ClaimedAt <= endUtc);
                if (userId.HasValue) q = q.Where(i => i.ClaimedBy == userId.Value);

                var rows = await q
                    .GroupBy(i => i.ClaimedBy)
                    .Select(g => new InvestmentClaimSummaryDto
                    {
                        UserId = g.Key,
                        Count = g.Count(),
                        TotalAmount = g.Sum(x => x.Amount)
                    })
                    .OrderByDescending(x => x.TotalAmount)
                    .ToListAsync();

                return new ApiResponse<List<InvestmentClaimSummaryDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentClaimSummaryDto>> { Success = false, Message = ex.Message };
            }
        }

        public async Task<ApiResponse<List<InvestmentClaimRowDto>>> GetClaimRows(DateTime startUtc, DateTime endUtc, long? userId)
        {
            try
            {
                var q = context.Investments.AsNoTracking()
                    .Where(i => i.ClaimedFully && i.ClaimedAt != null);

                q = q.Where(i => i.ClaimedAt >= startUtc && i.ClaimedAt <= endUtc);
                if (userId.HasValue) q = q.Where(i => i.ClaimedBy == userId.Value);

                var rows = await q
                    .OrderByDescending(i => i.ClaimedAt)
                    .Select(i => new InvestmentClaimRowDto
                    {
                        InvestmentId = i.Id,
                        CustomerCode = i.CustomerCode,
                        LocationId = i.LocationId,
                        Amount = i.Amount,
                        ClaimedAt = i.ClaimedAt ?? DateTime.UtcNow,
                        ClaimedBy = i.ClaimedBy,
                        ClaimNotes = i.ClaimNotes,
                        InvestmentTypeId = i.InvestmentTypeId,
                        StaffId = i.StaffId
                    })
                    .ToListAsync();

                return new ApiResponse<List<InvestmentClaimRowDto>> { Success = true, Data = rows };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<InvestmentClaimRowDto>> { Success = false, Message = ex.Message };
            }
        }
    }
}

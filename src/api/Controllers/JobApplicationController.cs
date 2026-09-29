using System.Security.Claims;
using JobAppTrackerApi.Data;
using JobAppTrackerApi.DTOs;
using JobAppTrackerApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobAppTrackerApi.Controllers
{
    [ApiController]
    [Route("api/applications")]
    [Authorize]
    public class JobApplicationController(AppDbContext db) : ControllerBase
    {
        private int _userId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost]
        public async Task<IActionResult> Create(CreateRequest request)
        {
            var item = new JobApplication
            {
                UserId = _userId,
                Title = request.Title,
                Company = request.Company,
                Description = request.Description,
                Link = request.Link,
                Status = AppStatus.Applied,
                AppliedDate = request.AppliedDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                UpdatedDate = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            db.JobApplications.Add(item);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = item.Id }, ToResponse(item));
        }

        [HttpGet]
        public async Task<ActionResult<List<JobAppResponse>>> Get(
                    [FromQuery] string? status,
                    [FromQuery] string? company,
                    [FromQuery] DateOnly? appliedAfter,
                    [FromQuery] DateOnly? appliedBefore,
                    [FromQuery] string? sortBy,
                    [FromQuery] string? sortDir,
                    [FromQuery] int page = 1,
                    [FromQuery] int pageSize = 10
            )
        {
            var query = db.JobApplications
                        .AsNoTracking()
                        .Where(app => app.UserId == _userId);

            if (!string.IsNullOrEmpty(status))
            {
                if (!Enum.TryParse<AppStatus>(status, true, out var parsedStatus) || !Enum.IsDefined(parsedStatus))
                    return BadRequest(new { message = $"Invalid status: {status}" });
                query = query.Where(app => app.Status == parsedStatus);
            }

            if (!string.IsNullOrEmpty(company))
            {
                query = query.Where(app => app.Company.Contains(company));
            }

            if (appliedAfter.HasValue)
            {
                query = query.Where(app => app.AppliedDate >= appliedAfter.Value);
            }

            if (appliedBefore.HasValue)
            {
                query = query.Where(app => app.AppliedDate <= appliedBefore.Value);
            }

            query = (sortBy?.ToLower(), sortDir?.ToLower()) switch
            {
                ("company", "asc") => query.OrderBy(app => app.Company),
                ("company", _) => query.OrderByDescending(app => app.Company),
                ("status", "asc") => query.OrderBy(app => app.Status),
                ("status", _) => query.OrderByDescending(app => app.Status),
                (_, "asc") => query.OrderBy(app => app.AppliedDate),
                _ => query.OrderByDescending(app => app.AppliedDate),
            };

            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(page, 1);

            var appCount = await query.CountAsync();
            var applications = await query
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

            return Ok(new {
                applications = applications.Select(ToResponse),
                page,
                pageSize,
                appCount,
                totalPages = (int)Math.Ceiling(appCount / (double)pageSize)
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<JobAppResponse>> GetById(int id)
        {
            var jobApp = await db.JobApplications.FirstOrDefaultAsync(app => app.Id == id && app.UserId == _userId);

            if (jobApp is null)
            {
                return NotFound();
            }

            return ToResponse(jobApp);
        }

        [HttpDelete]
        public async Task<IActionResult> Delete()
        {
            await db.JobApplications
                    .Where(a => a.UserId == _userId)
                    .ExecuteDeleteAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteById(int id)
        {
            var jobApp = await db.JobApplications.FirstOrDefaultAsync(app => app.Id == id && app.UserId == _userId);

            if (jobApp is null)
            {
                return NotFound();
            }

            db.Remove(jobApp);
            await db.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateById(int id, UpdateRequest request)
        {
            var jobApp = await db.JobApplications.FirstOrDefaultAsync(app => app.Id == id && app.UserId == _userId);

            if (jobApp is null)
            {
                return NotFound();
            }

            if (request.Title is not null)
            {
                jobApp.Title = request.Title;
            }

            if (request.Company is not null)
            {
                jobApp.Company = request.Company;
            }

            if (request.Description is not null)
            {
                jobApp.Description = request.Description;
            }

            if (request.Link is not null)
            {
                jobApp.Link = request.Link;
            }

            if (request.AppliedDate is not null)
            {
                jobApp.AppliedDate = request.AppliedDate.Value;
            }

            var entry = db.Entry(jobApp);
            bool hasChanges = entry.Properties
                                .Where(p => p.Metadata.Name != nameof(JobApplication.UpdatedDate))
                                .Any(p => p.IsModified);

            if (hasChanges)
                jobApp.UpdatedDate = DateOnly.FromDateTime(DateTime.UtcNow);

            await db.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var jobApp = await db.JobApplications.FirstOrDefaultAsync(app => app.Id == id && app.UserId == _userId);

            if (jobApp is null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (!Enum.TryParse<AppStatus>(status, ignoreCase: true, out var parsedStatus) || !Enum.IsDefined(parsedStatus))
                {
                    return BadRequest(new { message = $"Invalid status: {status}" });
                }

                jobApp.Status = parsedStatus;
            }
            else
            {
                return BadRequest(new { message = $"No status given" });
            }

            jobApp.UpdatedDate = DateOnly.FromDateTime(DateTime.UtcNow);

            await db.SaveChangesAsync();
            return NoContent();
        }

        private static JobAppResponse ToResponse(JobApplication jobApp)
        {
            return new JobAppResponse(
                jobApp.Id,
                jobApp.Title,
                jobApp.Company,
                jobApp.Description,
                jobApp.Link,
                jobApp.Status.ToString(),
                jobApp.AppliedDate,
                jobApp.UpdatedDate
            );
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KoyaClinicApi.Data;
using KoyaClinicApi.Models;
using KoyaClinicApi.Middleware;

namespace KoyaClinicApi.Controllers
{
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public BookingsController(AppDbContext db)
        {
            _db = db;
        }

        // ==========================================
        // LEVEL 1: POST /reset
        // ==========================================
        [HttpPost("/reset")]
        public async Task<IActionResult> Reset([FromBody] ResetRequest request)
        {
            // Delete everything
            _db.Bookings.RemoveRange(_db.Bookings);
            _db.IdempotencyRecords.RemoveRange(_db.IdempotencyRecords);
            _db.Slots.RemoveRange(_db.Slots);
            await _db.SaveChangesAsync();

            // Insert new slots
            var newSlots = request.Slots.Select(s => new Slot
            {
                Id = s.Id,
                Provider = s.Provider,
                Start = s.Start,
                End = s.End
            }).ToList();

            _db.Slots.AddRange(newSlots);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Slots replaced successfully" });
        }

        // ==========================================
        // LEVEL 1: GET /slots
        // ==========================================
        [HttpGet("/slots")]
        public async Task<IActionResult> GetSlots([FromQuery] string? provider)
        {
            var query = _db.Slots.AsQueryable();

            if (!string.IsNullOrEmpty(provider))
            {
                query = query.Where(s => s.Provider == provider);
            }

            // Earliest first
            var slots = await query.OrderBy(s => s.Start).ToListAsync();

            // Determine availability
            var bookedSlotIds = await _db.Bookings
                .Where(b => b.Status == "confirmed")
                .Select(b => b.SlotId)
                .ToListAsync();

            var response = slots.Select(s => new SlotResponse
            {
                Id = s.Id,
                Provider = s.Provider,
                Start = s.Start,
                End = s.End,
                Available = !bookedSlotIds.Contains(s.Id)
            }).ToList();

            return Ok(new { slots = response });
        }

        // ==========================================
        // LEVEL 1-5: POST /bookings
        // ==========================================
        [HttpPost("/bookings")]
        public async Task<IActionResult> CreateBooking([FromBody] BookingRequest request)
        {
            // Level 2: Validation
            if (string.IsNullOrWhiteSpace(request.SlotId) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ApiException(400, "slot_not_found"); // The spec implies this error if things are missing/invalid
            }

            // Level 4: Idempotency Check
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                var existingRecord = await _db.IdempotencyRecords.FindAsync(idempotencyKey);
                if (existingRecord != null)
                {
                    var existingBooking = await _db.Bookings.FindAsync(existingRecord.BookingId);
                    return StatusCode(201, MapToResponse(existingBooking!));
                }
            }

            // Level 5: Spam prevention (Max 5 bookings per email per minute)
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            var recentAttempts = await _db.Bookings
                .CountAsync(b => b.Email == request.Email && b.CreatedAt > oneMinuteAgo);

            if (recentAttempts >= 5)
            {
                throw new ApiException(429, "too many attempts");
            }

            // Level 3: Concurrency Control
            // We use a database transaction with UPDLOCK to stop double-booking
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Lock the slot row in SQL Server
                var slot = await _db.Slots
                    .FromSqlRaw("SELECT * FROM \"Slots\" WHERE \"Id\" = {0} FOR UPDATE", request.SlotId)
                    .FirstOrDefaultAsync();

                if (slot == null)
                {
                    throw new ApiException(404, "slot_not_found");
                }

                // Check if already booked (inside the lock)
                var isBooked = await _db.Bookings.AnyAsync(b => b.SlotId == request.SlotId && b.Status == "confirmed");
                if (isBooked)
                {
                    throw new ApiException(409, "slot_already_booked");
                }

                // Create the booking
                var booking = new Booking
                {
                    SlotId = request.SlotId,
                    Name = request.Name,
                    Email = request.Email,
                    Status = "confirmed",
                    CreatedAt = DateTime.UtcNow
                };

                _db.Bookings.Add(booking);

                // Save Idempotency key
                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    _db.IdempotencyRecords.Add(new IdempotencyRecord
                    {
                        Key = idempotencyKey,
                        BookingId = booking.Id
                    });
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return StatusCode(201, MapToResponse(booking));
            }
            catch (DbUpdateException) // Catch database unique constraint violations
            {
                await transaction.RollbackAsync();
                throw new ApiException(409, "slot_already_booked");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // ==========================================
        // LEVEL 4: GET /bookings (Pagination)
        // ==========================================
        [HttpGet("/bookings")]
        public async Task<IActionResult> GetBookings([FromQuery] string email, [FromQuery] int limit = 10, [FromQuery] string? cursor = null)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ApiException(400, "email is required");
            }

            if (limit < 1 || limit > 50)
            {
                throw new ApiException(400, "invalid limit");
            }

            // Base query
            var query = _db.Bookings
                .Where(b => b.Email.ToLower() == email.ToLower())
                .AsQueryable();

            // Cursor logic (Order by CreatedAt, then Id for uniqueness)
            if (!string.IsNullOrEmpty(cursor))
            {
                // Decode cursor (we are using the Last CreatedAt date as the cursor)
                if (DateTime.TryParse(cursor, out var cursorDate))
                {
                    query = query.Where(b => b.CreatedAt > cursorDate);
                }
            }

            // Oldest first
            var bookings = await query
                .OrderBy(b => b.CreatedAt)
                .ThenBy(b => b.Id)
                .Take(limit + 1) // Take 1 extra to see if there is a next page
                .ToListAsync();

            string? nextCursor = null;
            if (bookings.Count > limit)
            {
                bookings.RemoveAt(limit); // Remove the extra one
                nextCursor = bookings.Last().CreatedAt.ToString("O"); // ISO 8601 format
            }

            var response = new PaginatedBookingsResponse
            {
                Bookings = bookings.Select(MapToResponse).ToList(),
                NextCursor = nextCursor
            };

            return Ok(response);
        }

        // ==========================================
        // LEVEL 1: GET /bookings/{id}
        // ==========================================
        [HttpGet("/bookings/{id}")]
        public async Task<IActionResult> GetBooking(string id)
        {
            var booking = await _db.Bookings.FindAsync(id);
            if (booking == null)
            {
                throw new ApiException(404, "slot_not_found"); // Spec says "slot_not_found" for 404s
            }

            return Ok(MapToResponse(booking));
        }

        // ==========================================
        // LEVEL 3: DELETE /bookings/{id}
        // ==========================================
        [HttpDelete("/bookings/{id}")]
        public async Task<IActionResult> CancelBooking(string id)
        {
            var booking = await _db.Bookings.FindAsync(id);
            if (booking == null)
            {
                throw new ApiException(404, "slot_not_found"); // Spec says "slot_not_found" for 404s
            }

            if (booking.Status == "cancelled")
            {
                throw new ApiException(409, "slot_not_found"); // Already cancelled
            }

            booking.Status = "cancelled";
            await _db.SaveChangesAsync();

            return Ok(MapToResponse(booking));
        }

        // Helper method to map database model to exact API response format
        private BookingResponse MapToResponse(Booking booking)
        {
            return new BookingResponse
            {
                Id = booking.Id,
                SlotId = booking.SlotId,
                Name = booking.Name,
                Email = booking.Email,
                Status = booking.Status,
                CreatedAt = booking.CreatedAt
            };
        }
    }
}
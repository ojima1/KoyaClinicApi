using System.Text.Json.Serialization;

namespace KoyaClinicApi.Models
{
    // --- DATABASE ENTITIES ---

    public class Slot
    {
        public string Id { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public class Booking
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SlotId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = "confirmed";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class IdempotencyRecord
    {
        public string Key { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
    }

    // --- API REQUEST/RESPONSE MODELS (DTOs) ---

    // POST /reset
    public class ResetRequest
    {
        [JsonPropertyName("slots")]
        public List<SlotInput> Slots { get; set; } = new();
    }

    public class SlotInput
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("start")]
        public DateTime Start { get; set; }

        [JsonPropertyName("end")]
        public DateTime End { get; set; }
    }

    // GET /slots (Response)
    public class SlotResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("start")]
        public DateTime Start { get; set; }

        [JsonPropertyName("end")]
        public DateTime End { get; set; }

        [JsonPropertyName("available")]
        public bool Available { get; set; }
    }

    // POST /bookings
    public class BookingRequest
    {
        [JsonPropertyName("slot_id")]
        public string SlotId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    // GET /bookings/{id} and POST /bookings (Response)
    public class BookingResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("slot_id")]
        public string SlotId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    // GET /bookings (Response)
    public class PaginatedBookingsResponse
    {
        [JsonPropertyName("bookings")]
        public List<BookingResponse> Bookings { get; set; } = new();

        [JsonPropertyName("next_cursor")]
        public string? NextCursor { get; set; }
    }
}
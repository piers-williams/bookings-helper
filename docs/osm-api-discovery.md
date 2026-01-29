# OSM API Discovery

**Status:** Pending Manual Exploration

## Instructions

To complete this task, you need to reverse engineer the OSM (Online Scout Manager) API by observing network traffic in your browser:

### Steps:

1. **Open OSM in Browser**
   - Navigate to: https://www.onlinescoutmanager.co.uk
   - Log in with your credentials

2. **Open Browser DevTools**
   - Press F12 (Chrome/Edge) or Cmd+Option+I (Mac)
   - Go to the "Network" tab
   - Enable "Preserve log" checkbox

3. **Perform Actions and Document API Calls**
   - Navigate to bookings section
   - View a booking
   - Read comments
   - Note all API requests made

4. **Document Findings Below**

---

## Authentication

**Method:** [Cookie/Session/API Key - TBD]

**Required Headers:**
```
Authorization: [TBD]
Cookie: [TBD]
Other headers: [TBD]
```

**Login Process:**
- Login endpoint: [TBD]
- Response format: [TBD]
- Session management: [TBD]

---

## Endpoints Discovered

### GET /api/bookings (or similar)
**Purpose:** List bookings

**URL:** `[TBD]`

**Query Parameters:**
- `status`: [optional/required] - Filter by status (Provisional, Confirmed, etc.)
- `[other params]`: [TBD]

**Request Headers:**
```
[Document headers here]
```

**Sample Request:**
```bash
curl 'https://www.onlinescoutmanager.co.uk/api/...' \
  -H 'Authorization: ...' \
  -H 'Cookie: ...'
```

**Response Format:**
```json
{
  "example": "paste actual response here"
}
```

---

### GET /api/bookings/{id} (or similar)
**Purpose:** Get booking details

**URL:** `[TBD]`

**Path Parameters:**
- `id`: Booking ID

**Response Format:**
```json
{
  "example": "paste actual response here"
}
```

---

### GET /api/bookings/{id}/comments (or similar)
**Purpose:** Get comments for a booking

**URL:** `[TBD]`

**Response Format:**
```json
{
  "example": "paste actual response here"
}
```

---

### POST /api/bookings/{id}/comments (or similar)
**Purpose:** Add comment to booking (for Phase 2)

**URL:** `[TBD]`

**Request Body:**
```json
{
  "example": "document format here"
}
```

---

## Other Important Endpoints

### [Add any other endpoints you discover]

**Purpose:** [TBD]

**URL:** `[TBD]`

**Details:** [TBD]

---

## Field Mappings

Map OSM API fields to our database entities:

### Booking Fields
| OSM API Field | Our Field | Notes |
|--------------|-----------|-------|
| `id` | `OsmBookingId` | Unique booking identifier |
| `customer_name` | `CustomerName` | [TBD - verify actual field name] |
| `customer_email` | `CustomerEmail` | [TBD] |
| `start_date` | `StartDate` | [TBD - format?] |
| `end_date` | `EndDate` | [TBD] |
| `status` | `Status` | [TBD - possible values?] |

### Comment Fields
| OSM API Field | Our Field | Notes |
|--------------|-----------|-------|
| `id` | `OsmCommentId` | [TBD] |
| `author` | `AuthorName` | [TBD] |
| `text` | `TextPreview` | [TBD] |
| `created_at` | `CreatedDate` | [TBD - format?] |

---

## Rate Limiting

**Observed Limits:** [TBD]
- Requests per minute: [TBD]
- Requests per hour: [TBD]
- Any retry-after headers: [TBD]

**Recommendations:**
- [Add throttling strategy if needed]

---

## Pagination

**Method:** [Query params / Link headers / Cursor-based]

**Parameters:**
- `page`: [TBD]
- `limit`: [TBD]
- `offset`: [TBD]

**Example:**
```
[Document pagination example]
```

---

## Error Responses

**Common Error Codes:**
- 401: [Unauthorized - what triggers this?]
- 403: [Forbidden - what triggers this?]
- 404: [Not found]
- 429: [Rate limit exceeded]
- 500: [Server error]

**Error Response Format:**
```json
{
  "example": "paste actual error response"
}
```

---

## Notes and Observations

### Security Considerations
- [Note any CSRF tokens, session management, etc.]

### Data Quirks
- [Note any unusual data formats, edge cases, or inconsistencies]

### API Stability
- [Note any versioning, deprecation warnings, etc.]

### What Defines "Provisional"?
- [Document what makes a booking "provisional" vs "confirmed"]
- [Is there a specific status field? What are all possible values?]

### What Makes a Comment "New"?
- [Is there a flag? Or is it based on timestamp comparison?]
- [How do we track which comments we've already seen?]

---

## Next Steps

After completing this documentation:

1. Review findings with team
2. Implement OsmService in Task 11 based on these discoveries
3. Test with real OSM data
4. Update documentation with any corrections

---

**Last Updated:** [Date]
**Updated By:** [Your Name]
**OSM Version:** [Note the OSM version if visible]

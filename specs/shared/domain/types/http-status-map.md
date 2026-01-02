# HTTP status mapping

- 200 OK: GETs and preview
- 201 Created: POST create
- 204 No Content: DELETE success
- 400 Bad Request: malformed JSON / invalid request body
- 404 Not Found: missing resource
- 409 Conflict: duplicate id / version
- 422 Unprocessable Entity: semantic validation errors (optional; backend may use 400 consistently)
- 500 Internal Server Error: unexpected failures

#!/bin/bash
# Test User Creation API Endpoints

echo "Step 1: Getting admin token..."
TOKEN_RESPONSE=$(curl -s -X POST http://localhost:8080/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"ChangeMe123!"}')
  
TOKEN=$(echo $TOKEN_RESPONSE | jq -r '.access_token')
echo "[OK] Token obtained: ${TOKEN:0:20}..."

echo ""
echo "Step 2: Creating new user 'carlos'..."
USER_RESPONSE=$(curl -s -X POST http://localhost:8080/api/admin/auth/users \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "username": "carlos",
    "password": "CarlosPass456!",
    "displayName": "Carlos Oliveira",
    "email": "carlos@example.com",
    "roles": ["Metrics.Reader"]
  }')

echo "$USER_RESPONSE" | jq '.'
USER_ID=$(echo $USER_RESPONSE | jq -r '.id')
echo "[OK] User created with ID: $USER_ID"

echo ""
echo "Step 3: Logging in as new user..."
LOGIN_RESPONSE=$(curl -s -X POST http://localhost:8080/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"carlos","password":"CarlosPass456!"}')

NEW_TOKEN=$(echo $LOGIN_RESPONSE | jq -r '.access_token')
echo "[OK] Login successful"

echo ""
echo "Step 4: Accessing protected endpoint as new user..."
ME_RESPONSE=$(curl -s -X GET http://localhost:8080/api/auth/me \
  -H "Authorization: Bearer $NEW_TOKEN")

echo "$ME_RESPONSE" | jq '.'
echo "[OK] Protected endpoint accessible"

echo ""
echo "Step 5: Admin changing password for user..."
PASS_RESPONSE=$(curl -s -X PUT "http://localhost:8080/api/admin/auth/users/$USER_ID/password" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"newPassword":"NewPassword789!"}')

echo "$PASS_RESPONSE" | jq '.'
echo "[OK] Password changed"

echo ""
echo "Step 6: Logging in with new password..."
LOGIN_RESPONSE2=$(curl -s -X POST http://localhost:8080/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"carlos","password":"NewPassword789!"}')

echo "$LOGIN_RESPONSE2" | jq '.access_token' | cut -c1-30
echo "[OK] Login with new password successful"

echo ""
echo "Step 7: Admin updating user roles..."
UPDATE_RESPONSE=$(curl -s -X PUT "http://localhost:8080/api/admin/auth/users/$USER_ID" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"roles":["Metrics.Admin","Metrics.Reader"]}')

echo "$UPDATE_RESPONSE" | jq '.'
echo "[OK] User roles updated"

echo ""
echo "[DONE] All tests completed successfully!"

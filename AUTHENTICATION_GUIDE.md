# Authentication & Authorization Guide

## Quick Start

### 1. Start the Application
```bash
cd ITDeviceManager.API
dotnet run
```

The application will automatically:
- Apply database migrations (including Users table)
- Start listening on https://localhost:5001

### 2. Initialize Default Admin User

**First time only** - Create the default admin account:

```bash
POST https://localhost:5001/api/auth/init
```

Response:
```json
{
  "message": "Default admin user created successfully",
  "username": "admin",
  "warning": "Please change the default password immediately!"
}
```

Default credentials:
- Username: `admin`
- Password: `Admin@123`

⚠️ **IMPORTANT**: Change this password immediately after first login!

### 3. Login

```bash
POST https://localhost:5001/api/auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "Admin@123"
}
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "username": "admin"
}
```

Save this token - you'll need it for authenticated requests.

### 4. Use the Token

Include the token in the `Authorization` header for protected endpoints:

```bash
GET https://localhost:5001/api/devices/refresh
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

## Using Swagger UI

1. Navigate to https://localhost:5001/swagger
2. Click the **"Authorize"** button (lock icon) at the top right
3. Enter your token in the format: `Bearer YOUR_TOKEN_HERE`
4. Click **"Authorize"** then **"Close"**
5. All subsequent API calls will include the token automatically

## User Roles

The system has three roles with different permissions:

### Admin
- Full system access
- Can create/update/delete all resources
- Can register new users
- Can view all users

### Operator
- Can manage devices (create, update, discover)
- Can perform power operations (WOL, shutdown)
- Can manage device relations
- Cannot manage users

### User
- Read-only access
- Can view devices and relations
- Cannot modify anything

## API Endpoints

### Authentication Endpoints

#### Login
```
POST /api/auth/login
```
**Access**: Public
**Body**:
```json
{
  "username": "string",
  "password": "string"
}
```
**Returns**: JWT token and username

#### Initialize Default Admin
```
POST /api/auth/init
```
**Access**: Public (only works if no admin exists)
**Body**: None
**Returns**: Success message with default credentials

#### Register New User
```
POST /api/auth/register
```
**Access**: Admin only
**Body**:
```json
{
  "username": "string",
  "password": "string",
  "email": "string",
  "role": "Admin|Operator|User"
}
```
**Returns**: Created user details (without password hash)

#### Get All Users
```
GET /api/auth/users
```
**Access**: Admin only
**Returns**: List of all users with details

#### Get User by ID
```
GET /api/auth/{id}
```
**Access**: Admin only
**Returns**: User details

### Protected Device Endpoints

#### Refresh Devices (Ping or Discover)
```
POST /api/devices/refresh
```
**Access**: Admin, Operator
**Authorization Required**: Yes
**Body** (optional):
```json
{
  "networkRange": "192.168.1.1-254"
}
```

#### Discover Devices
```
POST /api/devices/discover
```
**Access**: Admin, Operator
**Authorization Required**: Yes

### Public Device Endpoints

#### Get All Devices
```
GET /api/devices
```
**Access**: Public
**Authorization Required**: No

#### Get Device by ID
```
GET /api/devices/{id}
```
**Access**: Public
**Authorization Required**: No

## Token Information

- **Algorithm**: HS256 (HMAC-SHA256)
- **Expiration**: 60 minutes (configurable)
- **Claims**: Username, Role, JTI (unique ID), IAT (issued at)
- **Validation**: Issuer, Audience, Lifetime, Signing Key

## Password Security

- **Algorithm**: PBKDF2 (Password-Based Key Derivation Function 2)
- **Hash Function**: HMAC-SHA256
- **Iterations**: 100,000
- **Salt Length**: 32 bytes (cryptographically random)
- **Hash Length**: 32 bytes
- **Storage Format**: Base64-encoded (salt + hash = 64 bytes total)

This makes password cracking extremely difficult even if the database is compromised.

## Configuration

Edit `appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "YOUR_SECURE_SECRET_KEY_AT_LEAST_32_CHARS",
    "Issuer": "ITDeviceManager",
    "Audience": "ITDeviceManagerAPI",
    "ExpirationMinutes": 60
  }
}
```

### Generate Secure Secret Key

**PowerShell**:
```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

**Bash/Linux**:
```bash
openssl rand -base64 32
```

**Node.js**:
```javascript
require('crypto').randomBytes(32).toString('base64')
```

## Common Scenarios

### Creating a New Operator User

1. Login as admin
2. Register new user:
```bash
POST /api/auth/register
Authorization: Bearer YOUR_ADMIN_TOKEN

{
  "username": "operator1",
  "password": "SecurePassword123!",
  "email": "operator1@company.com",
  "role": "Operator"
}
```

### Handling Token Expiration

When you receive a `401 Unauthorized` response:

1. Check if token is expired (default: 60 minutes)
2. Login again to get a new token
3. Update your application to store and refresh tokens automatically

**Future Enhancement**: Implement refresh token functionality for automatic token renewal without re-login.

### Troubleshooting Authentication

#### "401 Unauthorized" on Protected Endpoint
- Verify token is included in Authorization header
- Check token format: `Bearer {token}` (note the space)
- Ensure token hasn't expired
- Verify token signature is valid (check SecretKey in config)

#### "403 Forbidden" on Protected Endpoint
- User is authenticated but lacks required role
- Check endpoint's role requirements
- Verify user's role in database or via `GET /api/auth/users` (as admin)

#### "Invalid username or password"
- Verify credentials are correct
- Check user exists and is active in database
- Ensure password hash in database is valid

#### Cannot Initialize Admin User
- An admin user already exists
- Check database Users table
- If needed, manually update existing user to Admin role in database

## Security Best Practices

### Development
- Use default credentials for quick testing
- Keep JWT secret simple for debugging
- Allow permissive CORS for frontend development

### Production
1. **Change Default Password**: Never use "Admin@123" in production
2. **Secure JWT Secret**: Use cryptographically random key (32+ characters)
3. **HTTPS Only**: Never send tokens over unencrypted HTTP
4. **Restrict CORS**: Only allow specific frontend origins
5. **Rate Limiting**: Implement login attempt limits (not yet implemented)
6. **Audit Logging**: Log all authentication attempts (partially implemented)
7. **Password Policy**: Enforce strong passwords (not yet implemented)
8. **Session Management**: Consider implementing token revocation (not yet implemented)

## API Testing with curl

### Complete Workflow Example

```bash
# 1. Initialize admin (first time only)
curl -X POST https://localhost:5001/api/auth/init

# 2. Login
TOKEN=$(curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin@123"}' \
  | jq -r '.token')

# 3. Use token to refresh devices
curl -X POST https://localhost:5001/api/devices/refresh \
  -H "Authorization: Bearer $TOKEN"

# 4. Register new operator
curl -X POST https://localhost:5001/api/auth/register \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"username":"operator1","password":"Op3rator!Pass","role":"Operator"}'

# 5. View all users (admin only)
curl https://localhost:5001/api/auth/users \
  -H "Authorization: Bearer $TOKEN"
```

## Database Schema

### Users Table

| Column | Type | Constraints |
|--------|------|-------------|
| Id | int | Primary Key, Identity |
| Username | nvarchar(100) | Required, Unique |
| PasswordHash | nvarchar(256) | Required |
| Email | nvarchar(100) | Optional |
| Role | nvarchar(50) | Required, Default: "User" |
| IsActive | bit | Required, Default: true |
| CreatedAt | datetime2 | Required |
| LastLoginAt | datetime2 | Optional |

## Future Enhancements

- [ ] Refresh token implementation for automatic renewal
- [ ] Password reset functionality
- [ ] Email verification
- [ ] Two-factor authentication (2FA)
- [ ] Rate limiting for login attempts
- [ ] Password complexity requirements
- [ ] Token revocation / blacklist
- [ ] Audit log for all authentication events
- [ ] Account lockout after failed attempts
- [ ] Password expiration policy
- [ ] OAuth2 / OpenID Connect integration

## Support

For issues or questions:
- Check application logs for detailed error messages
- Verify JWT configuration in `appsettings.json`
- Ensure database migrations are applied
- Use Swagger UI for interactive API testing

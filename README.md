# OracleHRMinimalAPI

A minimal [ASP.NET](https://dotnet.microsoft.com/en-us/apps/aspnet) Core 8 Web API for querying the [Oracle HR sample schema](https://github.com/oracle-samples/db-sample-schemas), demonstrating JWT authentication, role-based access control (RBAC), and secure user management.

---

## Features

- Query employees, departments, and salary ranges from Oracle HR schema
- JWT authentication with short-lived access tokens
- Role-based authorization (Admin, Manager, User)
- User management endpoints for demo/testing
- Secure password hashing
- Supports refresh token extension (see below)

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Oracle Database](https://github.com/oracle-samples/db-sample-schemas) with HR schema enabled
- [Oracle.ManagedDataAccess.Core](https://www.nuget.org/packages/Oracle.ManagedDataAccess.Core/) NuGet package (already referenced)
- Update `appsettings.json` with your Oracle connection string and JWT secret

---

## Oracle HR Schema

- [Official Oracle HR Schema Documentation](https://docs.oracle.com/en/database/oracle/oracle-database/19/comsc/index.html)
- [Sample Scripts](https://github.com/oracle/db-sample-schemas)

---

## Getting Started

1. **Clone the repository**

   ```sh
   git clone <your-repo-url>
   cd Shinny/OracleHRMinimalAPI
   ```

2. **Configure your database connection**

   Edit `appsettings.json`:

   ```json
   {
     "ConnectionStrings": {
       "OracleDb": "User Id=hr;Password=yourpassword;Data Source=your_oracle_db"
     },
     "Jwt": {
       "Secret": "your-very-strong-secret-key"
     }
   }
   ```

3. **Restore packages and run the API**

   ```sh
   dotnet restore
   dotnet run
   ```

   The API will start on `http://localhost:5000` (or another port).

---

## API Usage

### 1. Seed a User

POST `/seed-user`

**Body:**
```json
{
  "username": "admin",
  "password": "admin123",
  "role": "Admin"
}
```

### 2. Login

POST `/login`

**Body:**
```json
{
  "username": "admin",
  "password": "admin123"
}
```

**Response:**
```json
{
  "token": "<JWT token>"
}
```

### 3. Call Protected Endpoints

Add header:
```
Authorization: Bearer <JWT token>
```

Example endpoints:
- `GET /employees` (any authenticated user)
- `GET /employees-salary` (Admin or Manager)
- `GET /employees-by-departments` (any authenticated user)
- `GET /now` (returns current date/time)

---

## Notes

- For demo, users are stored in-memory and lost on restart.
- Passwords are hashed using ASP.NET Core Identity.
- For production, use a persistent user store and secure your secrets.
- To implement refresh tokens, see [Microsoft Docs: Secure JWTs](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt?view=aspnetcore-8.0).

- [Oracle .NET Developer Center](https://www.oracle.com/database/technologies/appdev/dotnet.html) 

---

## License

MIT

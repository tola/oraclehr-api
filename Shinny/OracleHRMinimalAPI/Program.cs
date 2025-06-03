using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Example: Read sensitive value (e.g., JWT secret) from appsettings.json
string jwtSecret = configuration["Jwt:Secret"];
var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecret));
// JWT Authentication configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "OracleHRApi",
            ValidAudience = "OracleHRApiUsers",
            IssuerSigningKey = key
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole("Manager"));
});




var app = builder.Build();

async Task<List<Employee>> GetEmployeesBySalaryRange(IConfiguration configuration, decimal minSalary, decimal maxSalary)
{
    var employees = new List<Employee>();
    string connString = configuration.GetConnectionString("OracleDb");

    using var conn = new OracleConnection(connString);
    await conn.OpenAsync();

    using var cmd = new OracleCommand("HR.GetEmployeesBySalaryRange", conn)
    {
        CommandType = System.Data.CommandType.StoredProcedure
    };

    // Define procedure parameters
    cmd.Parameters.Add("p_min_salary", OracleDbType.Decimal).Value = minSalary;
    cmd.Parameters.Add("p_max_salary", OracleDbType.Decimal).Value = maxSalary;
    cmd.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

    using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        var employee = new Employee
        {
            EmployeeId = reader.GetInt32(0),
            FirstName = reader.GetString(1),
            LastName = reader.GetString(2),
            JobId = reader.GetString(3),
            JobTitle = reader.GetString(4),
            Salary = reader.GetDecimal(5),
            DepartmentName = reader.IsDBNull(6) ? null : reader.GetString(6),
            ManagerName = reader.IsDBNull(7) ? "No Manager" : reader.GetString(7),
            RegionName = reader.IsDBNull(8) ? "No Region" : reader.GetString(8),
            PhoneNumber = reader.IsDBNull(9) ? null : reader.GetString(9),
            Email = reader.IsDBNull(10) ? null : reader.GetString(10)
        };

        employees.Add(employee);
    }

    return employees;
}

#region Endpoints : Business Logic

app.MapGet("/now", () =>
{
    var now = DateTime.Now.ToString("dd-MMMM-yyyy hh:mm tt");
    return Results.Ok(new { now });
});

app.MapGet("/employees", [Authorize] async () =>
{

    // Connection string to connect to Oracle database
    string connString = configuration.GetConnectionString("OracleDb");

    var employees = new List<Employee>();
    using var conn = new OracleConnection(connString);
    await conn.OpenAsync();

    using var cmd = new OracleCommand(@"
                    SELECT e.EMPLOYEE_ID, e.FIRST_NAME, NVL(e.LAST_NAME, ' ') AS LAST_NAME, 
                        e.JOB_ID, e.SALARY, COALESCE(d.DEPARTMENT_NAME, 'No Department') AS DEPARTMENT_NAME,
                        COALESCE(m.FIRST_NAME || ' ' || m.LAST_NAME, 'No Manager') AS MANAGER_NAME
                    FROM HR.EMPLOYEES e
                    LEFT JOIN HR.DEPARTMENTS d ON e.DEPARTMENT_ID = d.DEPARTMENT_ID
                    LEFT JOIN HR.EMPLOYEES m ON e.MANAGER_ID = m.EMPLOYEE_ID

                        ", conn);

    using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        // Create Employee object using traditional instantiation
        var employee = new Employee();

        employee.EmployeeId = reader.GetInt32(0);
        employee.FirstName = reader.GetString(1);
        employee.LastName = reader.GetString(2);
        employee.JobId = reader.GetString(3);
        employee.Salary = reader.GetDecimal(4);
        employee.DepartmentName = reader.GetString(5);

        // Use GetValue to handle potential null values
        // and assign a default value if necessary
        var managerNameObj = reader.GetValue(6);
        employee.ManagerName = managerNameObj == DBNull.Value || string.IsNullOrWhiteSpace(managerNameObj.ToString())
            ? "No Manager"
            : managerNameObj.ToString();

        employees.Add(employee);
    }

    return Results.Ok(employees);
});


app.MapGet("/employees-salary", [Authorize(Policy = "ManagerOnly,AdminOnly")] async (decimal? minSalary, decimal? maxSalary) =>
{
    // Set default values if not provided
    decimal min = minSalary ?? 3000;
    decimal max = maxSalary ?? 10000;

    var employees = await GetEmployeesBySalaryRange(configuration, min, max);
    return Results.Ok(employees);
});



app.MapGet("/employees-by-departments", [Authorize] async () =>
{
    var departmentDataList = new List<DepartmentEmployeeCount>();
    string connString = configuration.GetConnectionString("OracleDb");

    using var conn = new OracleConnection(connString);
    await conn.OpenAsync();

    using var cmd = new OracleCommand("SELECT DEPARTMENT_NAME, DEPARTMENT_ID, NUM_EMPLOYEES FROM HR.DEPARTMENT_EMPLOYEE_COUNT_V", conn);

    using var reader = await cmd.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        var departmentData = new DepartmentEmployeeCount
        {
            DepartmentName = reader.GetString(0),
            DepartmentId = reader.GetInt32(1),
            NumEmployees = reader.GetInt32(2)
        };

        departmentDataList.Add(departmentData);
    }

    return Results.Ok(departmentDataList);
});



#endregion

#region Endpoints : User Management for demonstration purposes

// In-memory user store for demo purposes
var users = new List<(string Username, string PasswordHash, string Role)>();

// Endpoint to seed a user
app.MapPost("/seed-user", (SeedUserRequest req) =>
{
    var hasher = new PasswordHasher<string>();
    var passwordHash = hasher.HashPassword(req.Username, req.Password);

    if (users.Any(u => u.Username == req.Username))
        return Results.BadRequest("User already exists.");

    users.Add((req.Username, passwordHash, req.Role));
    return Results.Ok("User seeded.");
});

// Endpoint to log in and get JWT token
app.MapPost("/login", (string username, string password) =>
{
    var user = users.FirstOrDefault(u => u.Username == username);
    if (user == default)
        return Results.Unauthorized();

    var hasher = new PasswordHasher<string>();
    var result = hasher.VerifyHashedPassword(username, user.PasswordHash, password);
    if (result == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    // Create claims
    var claims = new[]
    {
        new Claim(ClaimTypes.Name, username),
        new Claim(ClaimTypes.Role, user.Role)
    };

    var jwt = new JwtSecurityToken(
        issuer: "OracleHRApi",
        audience: "OracleHRApiUsers",
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
    );

    var token = new JwtSecurityTokenHandler().WriteToken(jwt);
    return Results.Ok(new { token });
});


#endregion

app.UseAuthentication();
app.UseAuthorization();

app.Run();

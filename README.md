# Employee Task Tracker System

A task management system that lets an administrator create and assign work, and lets employees
track and update the work assigned to them. Built to the *Employee Task Tracker System*
specification.

Two roles share a single `Users` table and are separated by a `Role` column, exactly as the
specification requires: an **Admin** creates, assigns, edits and deletes tasks and sees
organisation-wide dashboard reporting; an **Employee** sees only the tasks assigned to them and
can update their status.

---

## Technology stack

| Layer | Choice |
|---|---|
| Frontend | **Blazor Server** (.NET 10) |
| Backend | **ASP.NET Core Web API** (.NET 10) |
| Database | **SQL Server** |
| Data access | **Stored procedures** via ADO.NET (`Microsoft.Data.SqlClient`) |
| Authentication | **JWT bearer tokens**, PBKDF2-SHA256 password hashing, role-based authorization |
| UI | Bootstrap 5 (bundled locally), responsive down to mobile |

The data access technology is stored procedures throughout. There is **no ORM and no inline SQL
anywhere in the codebase** — every database call is a `CommandType.StoredProcedure` invocation, and
all SQL lives in [`database/setup.sql`](database/setup.sql).

---

## Solution structure

```
EmployeeTaskTracker.sln
├── database/
│   └── setup.sql                      Schema, all stored procedures, and seed data
└── src/
    ├── EmployeeTaskTracker.Shared/    DTOs and constants shared by the API and the UI
    ├── EmployeeTaskTracker.Api/       ASP.NET Core Web API
    │   ├── Controllers/               Auth, Tasks, Dashboard, Users
    │   ├── Data/                      Repositories - stored procedure calls only
    │   ├── Security/                  Password hashing and JWT issuing
    │   └── Middleware/                Global exception handling and logging
    └── EmployeeTaskTracker.Web/       Blazor Server frontend
        ├── Components/Pages/          Login, Dashboard, Tasks
        ├── Components/Shared/         Reusable UI pieces
        └── Services/                  API client and authentication state
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (Express is fine) and SQL Server Management Studio
- Visual Studio 2022 or later, or any editor — the solution also builds from the command line

---

## Database Setup

1. Install SQL Server Express and SSMS (if not already installed)
2. Open SSMS, connect to your local instance
3. Run: `CREATE DATABASE EmployeeTaskTrackerDb`
4. Open `database/setup.sql`, ensure the first line is `USE [EmployeeTaskTrackerDb]`, and execute it
5. Update the connection string in `src/EmployeeTaskTracker.Api/appsettings.json` to point to your instance,
   e.g.

   ```
   Server=localhost\SQLEXPRESS;Database=EmployeeTaskTrackerDb;Trusted_Connection=True;TrustServerCertificate=True
   ```

The script creates the `Users` and `Tasks` tables, all eleven stored procedures, supporting indexes,
and seed data. It is safe to run more than once — procedures are recreated each time, while tables
and seed rows are only created if they are missing.

> **Compatibility note.** `setup.sql` is written against the SQL Server 2008 T-SQL feature set, so it
> runs unchanged on SQL Server 2008 through 2022 and on Azure SQL Database. It deliberately avoids
> `THROW`, `OFFSET/FETCH`, `IIF`, `CONCAT`, `FORMAT` and `TRY_CONVERT`, which all require SQL Server
> 2012 or later.

---

## Running the application

Both projects must be running: the Blazor frontend calls the Web API over HTTP.

### From Visual Studio

1. Open `EmployeeTaskTracker.sln`.
2. In the startup-project dropdown on the toolbar, choose **API + Web (both)**.
3. Press F5. Both projects start together and the browser opens on the UI.

The solution ships with a `.slnLaunch` profile that starts the API and the Blazor app together, so
no manual setup is needed.

> If the dropdown is not showing that profile, your Visual Studio version may predate multi-project
> launch profiles. Configure it by hand instead: right-click the **solution** → **Configure Startup
> Projects** → **Multiple startup projects** → set `EmployeeTaskTracker.Api` and
> `EmployeeTaskTracker.Web` to **Start**, and leave `EmployeeTaskTracker.Shared` on **None**.
>
> `EmployeeTaskTracker.Shared` is a class library and cannot be started on its own — selecting it as
> the startup project produces *"A project with an Output Type of Class Library cannot be started
> directly."*

### From the command line

Run each in its own terminal:

```bash
dotnet run --project src/EmployeeTaskTracker.Api
```

```bash
dotnet run --project src/EmployeeTaskTracker.Web
```

| Application | URL |
|---|---|
| Blazor Server UI | <http://localhost:5250> |
| Web API | <http://localhost:5080> |
| Swagger (API explorer) | <http://localhost:5080/swagger> |

If you change the API's port, update `ApiBaseUrl` in
`src/EmployeeTaskTracker.Web/appsettings.json` to match.

---

## Demo accounts

All seeded accounts use the password **`Password@123`**. The login page has buttons that fill these
in for you.

| Email | Role | Sees |
|---|---|---|
| `admin@tasktracker.com` | Admin | Every task, full create/edit/delete, org-wide dashboard |
| `arun@tasktracker.com` | Employee | Only their own tasks, status updates only |
| `divya@tasktracker.com` | Employee | Only their own tasks, status updates only |
| `karthik@tasktracker.com` | Employee | Only their own tasks, status updates only |

Passwords are stored as PBKDF2-SHA256 hashes (100,000 iterations, per-user random salt). The plain
password is never stored anywhere.

---

## Features

### Authentication
- Email and password login with client-side and server-side validation
- JWT issued by the API, carrying the user id, name, email and role
- Role-based authorization enforced on the API with `[Authorize(Roles = ...)]`
- Logout, and automatic redirect to login when a session expires
- Login failures return one generic message whether the email is unknown or the password is wrong,
  so the endpoint cannot be used to discover which accounts exist

### Dashboard
- Four summary cards: Total Tasks, Pending Tasks, Completed Tasks, High Priority
- Recent tasks list
- A completion percentage plus in-progress and overdue counts
- Figures are organisation-wide for an Admin and scoped to their own tasks for an Employee

### Task management
- Create, edit and delete tasks (Admin)
- Assign a task to any employee in the `Users` table
- Update task status (both roles — the only edit an Employee can make)
- Overdue tasks are highlighted
- Delete asks for confirmation first

### Search and filter
- Search by task title
- Filter by status and by priority, independently or together
- Search is debounced so typing does not fire a request per keystroke

---

## Security notes

- **Passwords** are hashed with PBKDF2-SHA256 and verified in fixed time.
- **Authorization is enforced on the API, not the UI.** Hiding a button is a convenience; every
  request is re-checked server-side against the token's role claim.
- **An Employee cannot reach another employee's data.** The API forces the assignee filter to the
  caller's own id, and `usp_Task_UpdateStatus` additionally scopes the `UPDATE` by assignee, so
  guessing a task id changes nothing.
- **SQL injection** is not possible: every call is a parameterised stored procedure.
- **The `returnUrl` on the login page** is restricted to paths inside the application, so a crafted
  link cannot bounce a freshly signed-in user to another site.
- **JWTs are validated** for issuer, audience, lifetime and signature, with zero clock skew.

Before deploying anywhere real, replace `Jwt:SigningKey` in `appsettings.json` with a fresh secret
and remove the seeded demo accounts.

---

## Requirements traceability

| Specification requirement | Where it is implemented |
|---|---|
| Blazor Server frontend | `src/EmployeeTaskTracker.Web` |
| ASP.NET Core Web API backend (.NET 9/10) | `src/EmployeeTaskTracker.Api`, targeting .NET 10 |
| SQL Server database | `database/setup.sql` |
| Data access via stored procedures | `Data/UserRepository.cs`, `Data/TaskRepository.cs` |
| User login / logout | `Components/Pages/Login.razor`, `AuthController` |
| JWT authentication | `Security/JwtTokenService.cs`, `Program.cs` |
| Role-based authorization | `[Authorize(Roles = ...)]` on `TasksController`, `UsersController` |
| Password hashing | `Security/PasswordHasher.cs` |
| Total / Pending / Completed / High Priority counts | `usp_Dashboard_GetStats`, `Dashboard.razor` |
| Task statistics cards, recent tasks list | `Dashboard.razor` |
| Create / Edit / Delete task | `TasksController`, `Tasks.razor`, `TaskEditor.razor` |
| Assign tasks to users in the Users table | `usp_Task_Insert`, `usp_Task_Update` |
| Update task status | `usp_Task_UpdateStatus`, status dropdown in `Tasks.razor` |
| Employee details in a single Users table, split by Role | `Users` table, `CK_Users_Role` |
| Search by title, filter by status and priority | `usp_Task_Search`, `Tasks.razor` |
| API response under 2 seconds | Indexed queries, single-pass aggregates, parallel dashboard reads |
| Responsive, user-friendly UI | `wwwroot/app.css`, Bootstrap 5 |
| Proper exception handling | `Middleware/ExceptionHandlingMiddleware.cs` |
| Logging support | `ILogger` throughout the API |
| Login page, Dashboard page, Task Management page | `Components/Pages/` |
| Users and Tasks table design | `database/setup.sql` |

---

## Database design

**Users**

| Column | Type | Notes |
|---|---|---|
| UserId | int | Identity, primary key |
| Name | nvarchar(100) | |
| Email | nvarchar(256) | Unique |
| PasswordHash | nvarchar(256) | PBKDF2-SHA256 |
| Role | nvarchar(20) | `Admin` or `Employee`, enforced by a check constraint |
| IsActive | bit | Lets an account be disabled without deleting history |
| CreatedAt | datetime | |

**Tasks**

| Column | Type | Notes |
|---|---|---|
| TaskId | int | Identity, primary key |
| Title | nvarchar(200) | |
| Description | nvarchar(1000) | Optional |
| AssignedTo | int | FK to `Users`, nullable so a task can start unassigned |
| Priority | nvarchar(20) | `Low`, `Medium`, `High` |
| Status | nvarchar(20) | `Pending`, `InProgress`, `Completed` |
| DueDate | datetime | Optional |
| CreatedBy | int | FK to `Users` |
| CreatedAt / UpdatedAt | datetime | Audit trail |

`IsActive`, `CreatedBy`, `CreatedAt` and `UpdatedAt` are additions beyond the columns listed in the
specification; every column the specification names is present with the type it specifies.

### Stored procedures

| Procedure | Purpose |
|---|---|
| `usp_User_GetByEmail` | Login lookup |
| `usp_User_GetById` | Profile lookup |
| `usp_User_GetEmployees` | Populates the assignee dropdown |
| `usp_Task_Search` | Task list with optional search, status, priority and assignee filters |
| `usp_Task_GetById` | Single task |
| `usp_Task_Insert` | Create |
| `usp_Task_Update` | Full edit |
| `usp_Task_UpdateStatus` | Status-only update, scoped by assignee |
| `usp_Task_Delete` | Delete |
| `usp_Dashboard_GetStats` | The four summary cards in a single pass |
| `usp_Dashboard_GetRecentTasks` | Recent tasks widget |

---

## API reference

All endpoints except `POST /api/auth/login` require an `Authorization: Bearer <token>` header.

| Method | Endpoint | Role | Description |
|---|---|---|---|
| POST | `/api/auth/login` | Anonymous | Exchange credentials for a JWT |
| GET | `/api/auth/me` | Any | Current user profile |
| GET | `/api/dashboard` | Any | Summary stats and recent tasks |
| GET | `/api/tasks` | Any | List tasks; `?search=&status=&priority=&assignedTo=` |
| GET | `/api/tasks/{id}` | Any | Single task |
| POST | `/api/tasks` | Admin | Create |
| PUT | `/api/tasks/{id}` | Admin | Update |
| PATCH | `/api/tasks/{id}/status` | Any | Update status only |
| DELETE | `/api/tasks/{id}` | Admin | Delete |
| GET | `/api/users/employees` | Admin | Employees available for assignment |
| GET | `/health` | Anonymous | Readiness probe |

Swagger UI at `/swagger` has an **Authorize** button — paste a token from `/api/auth/login` to try
the secured endpoints.

---

## Troubleshooting

**"The database is unavailable"** — SQL Server is not running or the connection string is wrong.
Check the SQL Server service and the `DefaultConnection` value in the API's `appsettings.json`.

**"Could not reach the Employee Task Tracker API"** — the API project is not running, or `ApiBaseUrl`
in the Web project's `appsettings.json` does not match the port the API is listening on.

**"Invalid object name 'dbo.Users'"** — `setup.sql` has not been run against the database named in
the connection string.

**Login fails with the demo password** — the `Users` table was seeded by something other than
`setup.sql`. Delete the rows and re-run the script so the seeded hashes match.

# Employee Task Tracker System

A task management system that lets an administrator create and assign work, and lets employees
track and update the work assigned to them. Built to the *Employee Task Tracker System*
specification.

Two roles share a single `Users` table and are separated by a `Role` column, exactly as the
specification requires: an **Admin** creates, assigns, edits and deletes tasks and sees
organisation-wide dashboard reporting; an **Employee** sees only the tasks assigned to them and
can update their status.

---

## Getting started

From a clean machine to a running application. Allow about ten minutes, most of it installing
prerequisites you may already have.

> **The one thing to know up front:** this is two applications, not one. The Blazor UI calls a
> separate Web API over HTTP, so **both projects must be running**. Step 6 covers how to start them
> together.

### Step 1 — Check what you already have

Open a terminal and run:

```bash
dotnet --list-sdks
```

You need a **9.0.x** entry. If the command is not recognised, or no 9.0 line appears, install the
[.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

You also need **SQL Server** (Express edition is fine) and a way to run a `.sql` script against it.
Either works: [SQL Server Management Studio](https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms),
or the **SQL Server Object Explorer** built into Visual Studio — step 4 covers both, so SSMS is not
required if you have Visual Studio.

Visual Studio is optional. The solution builds and runs entirely from the command line, but if you
use it, **Visual Studio 2022 version 17.12 or later, or Visual Studio 2026**. The project targets
.NET 9, which older 17.x releases cannot open.

### Step 2 — Get the code

```bash
git clone https://github.com/Akil1502/EmployeeTaskTracker.git
```

Or download the ZIP from the green **Code** button on GitHub and extract it.

### Step 3 — Find your SQL Server instance name

You need this for the next two steps. Open SSMS: the value in the **Server name** box on the connect
dialog is your instance name — commonly `localhost\SQLEXPRESS`, sometimes just `localhost` or
`(localdb)\MSSQLLocalDB`.

If you would rather ask the server, run this in any query window:

```sql
SELECT @@SERVERNAME;
```

### Step 4 — Create the database and run the script

Use whichever tool you prefer — both do exactly the same thing.

#### Option A — SQL Server Management Studio

1. Connect to your instance.
2. Open a new query window and run:

   ```sql
   CREATE DATABASE EmployeeTaskTrackerDb;
   ```

3. Open `database/setup.sql` from the cloned folder (**File → Open → File**).
4. Check the `USE [EmployeeTaskTrackerDb]` line at the top matches the database you just created.
5. Press **F5** to execute.

#### Option B — Visual Studio

Visual Studio can do all of this without SSMS installed.

1. Open **View → SQL Server Object Explorer**.
2. Expand **SQL Server**. If your instance is not listed, right-click **SQL Server → Add SQL Server**
   and connect to it.
3. Right-click the instance's **Databases** node → **Add New Database**, and name it
   `EmployeeTaskTrackerDb`.
4. Open `database/setup.sql` — it is in the **database** folder in Solution Explorer once you have
   opened `EmployeeTaskTracker.sln`.
5. Use the database dropdown on the SQL toolbar to point the query at `EmployeeTaskTrackerDb`, then
   click **Execute** (or press **Ctrl+Shift+E**).

> If the toolbar does not appear, right-click the editor and choose **Connection → Connect**, pick
> your instance and the `EmployeeTaskTrackerDb` database, then execute.

#### What the script does, and confirming it worked

It creates both tables, all eleven stored procedures, the supporting indexes and the demo data. It is
safe to run more than once: procedures are recreated every time, while tables and seed rows are only
created if they are missing, so re-running it never destroys data you have entered.

Run this in the same query window to confirm:

```sql
USE EmployeeTaskTrackerDb;
SELECT (SELECT COUNT(*) FROM dbo.Users)      AS Users,
       (SELECT COUNT(*) FROM dbo.Tasks)      AS Tasks,
       (SELECT COUNT(*) FROM sys.procedures) AS Procedures;
```

You should see **4 users, 8 tasks and 11 procedures**.

### Step 5 — Point the application at your database

Open `src/EmployeeTaskTracker.Api/appsettings.json`. It ships with:

```
Server=localhost\SQLEXPRESS;Database=EmployeeTaskTrackerDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False
```

**If your instance is `localhost\SQLEXPRESS`, skip this step.** Otherwise replace the `Server=` part
with the name from step 3. Ready-made strings for a default instance, LocalDB, SQL Server
authentication and a remote server are in
[If the default connection string does not work](#if-the-default-connection-string-does-not-work).

### Step 6 — Run both projects

This application is a Blazor UI **and** a Web API. Both have to be running, so this step needs one
deliberate action rather than just pressing F5.

#### From Visual Studio

Open `EmployeeTaskTracker.sln`, then do **one** of the following.

**Either — use the bundled launch profile (quickest)**

Click the dropdown next to the green ▶ button on the toolbar and choose **API + Web (both)**, then
press **F5**.

The solution includes this profile, so it is already in the list. You do still have to pick it:
Visual Studio remembers your choice in a per-user file that is deliberately not committed, so on a
fresh clone it defaults to a single project instead.

**Or — configure it by hand (works in every Visual Studio version)**

1. Right-click the **solution** at the top of Solution Explorer — the `Solution 'EmployeeTaskTracker'`
   node, not one of the projects.
2. Choose **Configure Startup Projects…** (older versions call it **Set Startup Projects…**).
3. Select **Multiple startup projects** and set the Action column to:

   | Project | Action |
   |---|---|
   | `EmployeeTaskTracker.Api` | **Start** |
   | `EmployeeTaskTracker.Web` | **Start** |
   | `EmployeeTaskTracker.Shared` | **None** |

4. Click **OK**, then press **F5**.

This choice is remembered, so it is a one-time step.

> `EmployeeTaskTracker.Shared` is a class library holding the types the other two projects share. It
> compiles to a `.dll` and cannot be started. Selecting it produces *"A project with an Output Type
> of Class Library cannot be started directly."*

**How to tell it worked:** two browser tabs open, or one tab plus a console window. The UI is at
<http://localhost:5250> and shows the sign-in page. If you only get Swagger at
<http://localhost:5080/swagger>, only the API started — go back and set both projects to **Start**.

#### From the command line

Open two terminals and run one in each.

```bash
dotnet run --project src/EmployeeTaskTracker.Api
```

```bash
dotnet run --project src/EmployeeTaskTracker.Web
```

Then browse to <http://localhost:5250>.

### Step 7 — Sign in

| Email | Password | Role |
|---|---|---|
| `admin@tasktracker.com` | `Password@123` | Admin |
| `arun@tasktracker.com` | `Password@123` | Employee |

The login page has buttons that fill either account in for you.

### Step 8 — Worth trying

A short tour that exercises everything the specification asks for:

1. **Sign in as the admin.** The dashboard shows organisation-wide figures.
2. **Click a summary card** — say *High Priority*. It opens the task list already filtered.
3. **Add a task** from Task Management, assign it to an employee and give it a due date.
4. **Search a person's name** in the top bar, press Enter. Search matches task titles and assignees.
5. **Filter** by status and priority together, then clear the filters.
6. **Edit and delete** the task you created. Deleting asks for confirmation.
7. **Sign out and sign in as `arun@tasktracker.com`.** Note that the dashboard now counts only his
   work, the task list shows only his tasks, and the add, edit and delete controls are gone — he can
   change status and nothing else.
8. **Open <http://localhost:5080/swagger>** to exercise the Web API directly. Paste the token from
   `POST /api/auth/login` into the **Authorize** box to call the secured endpoints.

If anything does not work, [Troubleshooting](#troubleshooting) covers the common causes.

---

## Technology stack

| Layer | Choice |
|---|---|
| Frontend | **Blazor Server** (.NET 9) |
| Backend | **ASP.NET Core Web API** (.NET 9) |
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

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server (Express is fine) and SQL Server Management Studio
- Visual Studio 2022 (17.12 or later) or Visual Studio 2026 — or no IDE at all, since the solution
  builds and runs from the command line

> .NET 9 was chosen over .NET 10 deliberately: .NET 10 projects cannot be opened in Visual Studio
> 2022 at all, and the specification permits either. Targeting .NET 9 means the solution opens in
> both Visual Studio 2022 and 2026.

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

### If the default connection string does not work

The value shipped in `appsettings.json` assumes a SQL Server **Express** instance named `SQLEXPRESS`
on the local machine. That is the most common setup, but not the only one. Replace
`ConnectionStrings:DefaultConnection` in `src/EmployeeTaskTracker.Api/appsettings.json` with whichever
line below matches your machine.

**To find your instance name:** it is the exact text in the *Server name* box when you connect in
SSMS. You can also run `SELECT @@SERVERNAME` in any query window.

| Your setup | Connection string |
|---|---|
| SQL Server Express (default, shipped) | `Server=localhost\SQLEXPRESS;Database=EmployeeTaskTrackerDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False` |
| Default instance (no instance name) | `Server=localhost;Database=EmployeeTaskTrackerDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False` |
| LocalDB (ships with Visual Studio) | `Server=(localdb)\MSSQLLocalDB;Database=EmployeeTaskTrackerDb;Trusted_Connection=True;TrustServerCertificate=True` |
| A named instance | `Server=YOUR-PC\YOUR-INSTANCE;Database=EmployeeTaskTrackerDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False` |
| SQL Server authentication instead of Windows | `Server=localhost\SQLEXPRESS;Database=EmployeeTaskTrackerDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;Encrypt=False` |
| A remote or hosted server | `Server=your.server.address,1433;Database=EmployeeTaskTrackerDb;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True` |

Notes on the individual settings:

- **`Trusted_Connection=True`** uses your Windows account. Swap it for `User Id=...;Password=...` if
  your server uses SQL Server authentication.
- **`TrustServerCertificate=True`** accepts the server's self-signed certificate. It is appropriate
  for a local development instance and should not be used against a production server.
- **`Encrypt=False`** skips TLS negotiation. It is included because `Microsoft.Data.SqlClient` 4.0 and
  later default to `Encrypt=True`, which can fail against older servers that predate TLS 1.2. If your
  server negotiates TLS happily you can drop it; it is a safe default either way.
- **`Database=`** must match the database you created in step 3 and the `USE [...]` line at the top of
  `setup.sql`. If you prefer a different name, change it in all three places.

> **The database has to exist on the instance the connection string points at.** Changing the server
> in `appsettings.json` does not move the data. If you switch from `SQLEXPRESS` to LocalDB, for
> example, you must run `CREATE DATABASE` and `setup.sql` again against LocalDB — otherwise the API
> reports *Cannot open database "EmployeeTaskTrackerDb" requested by the login*, which means it
> reached the server but the database is not there.

If the API starts but every page reports *"The database is unavailable"*, the connection string is
almost always the cause. Confirm you can connect to the same instance in SSMS using the same
credentials, and that `EmployeeTaskTrackerDb` is listed under that instance.

> **Compatibility note.** `setup.sql` is written against the SQL Server 2008 T-SQL feature set, so it
> runs unchanged on SQL Server 2008 through 2022 and on Azure SQL Database. It deliberately avoids
> `THROW`, `OFFSET/FETCH`, `IIF`, `CONCAT`, `FORMAT` and `TRY_CONVERT`, which all require SQL Server
> 2012 or later.

---

## Running the application

Both projects must be running: the Blazor frontend calls the Web API over HTTP.

### From Visual Studio

Two equivalent routes. [Step 6](#step-6--run-both-projects) of the guide above walks through both in
full detail; this is the summary.

**The bundled launch profile.** Open `EmployeeTaskTracker.sln`, pick **API + Web (both)** from the
dropdown beside the green ▶ button, and press F5. The profile is committed as `.slnLaunch`, so it is
always in the list — but it is not selected for you. Visual Studio keeps the current startup
selection in a per-user `.suo` file, which is not committed, so a fresh clone falls back to a single
project until you choose.

**Configuring it by hand.** Right-click the **solution** node → **Configure Startup Projects** →
**Multiple startup projects** → set `EmployeeTaskTracker.Api` and `EmployeeTaskTracker.Web` to
**Start**, and leave `EmployeeTaskTracker.Shared` on **None**. Visual Studio remembers this, so it is
a one-time step.

> `EmployeeTaskTracker.Shared` is a class library and cannot be started on its own — selecting it as
> the startup project produces *"A project with an Output Type of Class Library cannot be started
> directly."*

If only one project starts you will land on Swagger at <http://localhost:5080/swagger> with no UI, or
on a UI that reports *"Could not reach the Employee Task Tracker API"*. Either way, the fix is to set
both projects to **Start**.

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

The session is held in the browser's encrypted session storage, so refreshing the page keeps you
signed in while a new tab or a restarted browser returns you to the login page.

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
- Search by task title or by the name of the employee the task is assigned to, from the task page
  or the search box in the top bar
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
| ASP.NET Core Web API backend (.NET 9/10) | `src/EmployeeTaskTracker.Api`, targeting .NET 9 |
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

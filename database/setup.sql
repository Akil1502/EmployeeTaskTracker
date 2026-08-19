/*
================================================================================
  Employee Task Tracker System - Database Setup Script
================================================================================
  Creates the schema (Users, Tasks) and every stored procedure the application
  uses. All data access in the API goes through these procedures - there is no
  inline SQL and no ORM anywhere in the codebase.

  COMPATIBILITY
  -------------
  This script is written against the SQL Server 2008 T-SQL feature set so that
  it runs unchanged on SQL Server 2008 through 2022 and on Azure SQL Database.
  It therefore avoids THROW, OFFSET/FETCH, IIF, CONCAT, FORMAT and TRY_CONVERT,
  all of which require SQL Server 2012 or later.

  HOW TO RUN
  ----------
  1. Open SQL Server Management Studio and connect to your instance.
  2. Run:  CREATE DATABASE EmployeeTaskTrackerDb
  3. Make sure the USE statement below names that database, then execute this
     whole script (F5).

  The script is re-runnable: it drops and recreates the procedures every time,
  and only creates the tables and seed rows if they do not already exist.
================================================================================
*/

USE [EmployeeTaskTrackerDb];
GO

SET NOCOUNT ON;
GO

/*
================================================================================
  SECTION 1 - TABLES
================================================================================
*/

-- ---------------------------------------------------------------------------
-- Users: a single table holds both Admin and Employee accounts, as required by
-- the specification. The two are distinguished only by the Role column.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        UserId       INT             IDENTITY(1,1) NOT NULL,
        Name         NVARCHAR(100)   NOT NULL,
        Email        NVARCHAR(256)   NOT NULL,
        PasswordHash NVARCHAR(256)   NOT NULL,
        Role         NVARCHAR(20)    NOT NULL,
        IsActive     BIT             NOT NULL CONSTRAINT DF_Users_IsActive  DEFAULT (1),
        CreatedAt    DATETIME        NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (GETDATE()),

        CONSTRAINT PK_Users        PRIMARY KEY CLUSTERED (UserId),
        CONSTRAINT UQ_Users_Email  UNIQUE (Email),
        CONSTRAINT CK_Users_Role   CHECK (Role IN ('Admin', 'Employee'))
    );

    PRINT 'Created table dbo.Users';
END
GO

-- ---------------------------------------------------------------------------
-- Tasks: AssignedTo is a foreign key back into Users. It is nullable so that an
-- Admin can create a task before deciding who owns it.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.Tasks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tasks
    (
        TaskId      INT             IDENTITY(1,1) NOT NULL,
        Title       NVARCHAR(200)   NOT NULL,
        Description NVARCHAR(1000)  NULL,
        AssignedTo  INT             NULL,
        Priority    NVARCHAR(20)    NOT NULL,
        Status      NVARCHAR(20)    NOT NULL,
        DueDate     DATETIME        NULL,
        CreatedBy   INT             NULL,
        CreatedAt   DATETIME        NOT NULL CONSTRAINT DF_Tasks_CreatedAt DEFAULT (GETDATE()),
        UpdatedAt   DATETIME        NULL,

        CONSTRAINT PK_Tasks             PRIMARY KEY CLUSTERED (TaskId),
        CONSTRAINT FK_Tasks_AssignedTo  FOREIGN KEY (AssignedTo) REFERENCES dbo.Users (UserId),
        CONSTRAINT FK_Tasks_CreatedBy   FOREIGN KEY (CreatedBy)  REFERENCES dbo.Users (UserId),
        CONSTRAINT CK_Tasks_Priority    CHECK (Priority IN ('Low', 'Medium', 'High')),
        CONSTRAINT CK_Tasks_Status      CHECK (Status   IN ('Pending', 'InProgress', 'Completed'))
    );

    PRINT 'Created table dbo.Tasks';
END
GO

-- Indexes supporting the dashboard aggregates and the search/filter screen.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_AssignedTo' AND object_id = OBJECT_ID('dbo.Tasks'))
    CREATE NONCLUSTERED INDEX IX_Tasks_AssignedTo ON dbo.Tasks (AssignedTo);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tasks_Status_Priority' AND object_id = OBJECT_ID('dbo.Tasks'))
    CREATE NONCLUSTERED INDEX IX_Tasks_Status_Priority ON dbo.Tasks (Status, Priority);
GO


/*
================================================================================
  SECTION 2 - STORED PROCEDURES: USERS / AUTHENTICATION
================================================================================
*/

-- ---------------------------------------------------------------------------
-- usp_User_GetByEmail
-- Used by the login flow. Returns the stored hash so the API can verify the
-- supplied password; the hash never leaves the API layer.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_User_GetByEmail', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_User_GetByEmail;
GO
CREATE PROCEDURE dbo.usp_User_GetByEmail
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  u.UserId,
            u.Name,
            u.Email,
            u.PasswordHash,
            u.Role,
            u.IsActive
    FROM    dbo.Users AS u
    WHERE   u.Email = @Email;
END
GO

-- ---------------------------------------------------------------------------
-- usp_User_GetEmployees
-- Feeds the "Assign to" dropdown on the task form.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_User_GetEmployees', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_User_GetEmployees;
GO
CREATE PROCEDURE dbo.usp_User_GetEmployees
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  u.UserId,
            u.Name,
            u.Email,
            u.Role
    FROM    dbo.Users AS u
    WHERE   u.Role = 'Employee'
      AND   u.IsActive = 1
    ORDER BY u.Name;
END
GO

-- ---------------------------------------------------------------------------
-- usp_User_GetById
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_User_GetById', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_User_GetById;
GO
CREATE PROCEDURE dbo.usp_User_GetById
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  u.UserId,
            u.Name,
            u.Email,
            u.Role,
            u.IsActive
    FROM    dbo.Users AS u
    WHERE   u.UserId = @UserId;
END
GO


/*
================================================================================
  SECTION 3 - STORED PROCEDURES: TASK MANAGEMENT
================================================================================
*/

-- ---------------------------------------------------------------------------
-- usp_Task_Search
-- Backs the Task Management grid. Every filter argument is optional: passing
-- NULL means "do not filter on this column".
--
-- @Search matches either the task title or the name of the employee it is
-- assigned to, so one box finds "safety audit" and "Divya" alike.
--
-- @AssignedTo is how role-based visibility is enforced - the API passes the
-- caller's own UserId when that caller is an Employee, so an Employee can only
-- ever read their own tasks. It is applied independently of @Search, so
-- searching for a colleague's name cannot widen what an Employee can see.
--
-- OPTION (RECOMPILE) keeps the optional-parameter pattern from reusing a plan
-- built for a different combination of filters.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Task_Search', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Task_Search;
GO
CREATE PROCEDURE dbo.usp_Task_Search
    @Search     NVARCHAR(200) = NULL,
    @Status     NVARCHAR(20)  = NULL,
    @Priority   NVARCHAR(20)  = NULL,
    @AssignedTo INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  t.TaskId,
            t.Title,
            t.Description,
            t.AssignedTo,
            a.Name AS AssignedToName,
            t.Priority,
            t.Status,
            t.DueDate,
            t.CreatedBy,
            c.Name AS CreatedByName,
            t.CreatedAt,
            t.UpdatedAt
    FROM    dbo.Tasks AS t
            LEFT JOIN dbo.Users AS a ON a.UserId = t.AssignedTo
            LEFT JOIN dbo.Users AS c ON c.UserId = t.CreatedBy
    WHERE   (@Search     IS NULL
             OR t.Title LIKE '%' + @Search + '%'
             OR a.Name  LIKE '%' + @Search + '%')
      AND   (@Status     IS NULL OR t.Status = @Status)
      AND   (@Priority   IS NULL OR t.Priority = @Priority)
      AND   (@AssignedTo IS NULL OR t.AssignedTo = @AssignedTo)
    ORDER BY
            -- Overdue and soon-due work first, then newest.
            CASE WHEN t.Status = 'Completed' THEN 1 ELSE 0 END,
            CASE WHEN t.DueDate IS NULL THEN 1 ELSE 0 END,
            t.DueDate,
            t.TaskId DESC
    OPTION (RECOMPILE);
END
GO

-- ---------------------------------------------------------------------------
-- usp_Task_GetById
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Task_GetById', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Task_GetById;
GO
CREATE PROCEDURE dbo.usp_Task_GetById
    @TaskId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  t.TaskId,
            t.Title,
            t.Description,
            t.AssignedTo,
            a.Name AS AssignedToName,
            t.Priority,
            t.Status,
            t.DueDate,
            t.CreatedBy,
            c.Name AS CreatedByName,
            t.CreatedAt,
            t.UpdatedAt
    FROM    dbo.Tasks AS t
            LEFT JOIN dbo.Users AS a ON a.UserId = t.AssignedTo
            LEFT JOIN dbo.Users AS c ON c.UserId = t.CreatedBy
    WHERE   t.TaskId = @TaskId;
END
GO

-- ---------------------------------------------------------------------------
-- usp_Task_Insert
-- Returns the new TaskId to the caller.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Task_Insert', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Task_Insert;
GO
CREATE PROCEDURE dbo.usp_Task_Insert
    @Title       NVARCHAR(200),
    @Description NVARCHAR(1000) = NULL,
    @AssignedTo  INT            = NULL,
    @Priority    NVARCHAR(20),
    @Status      NVARCHAR(20),
    @DueDate     DATETIME       = NULL,
    @CreatedBy   INT            = NULL,
    @NewTaskId   INT            OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Reject an assignment to a user that does not exist or is not an Employee.
    IF @AssignedTo IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @AssignedTo AND IsActive = 1)
    BEGIN
        RAISERROR ('The selected assignee does not exist or is inactive.', 16, 1);
        RETURN;
    END

    INSERT INTO dbo.Tasks (Title, Description, AssignedTo, Priority, Status, DueDate, CreatedBy)
    VALUES (@Title, @Description, @AssignedTo, @Priority, @Status, @DueDate, @CreatedBy);

    SET @NewTaskId = CAST(SCOPE_IDENTITY() AS INT);
END
GO

-- ---------------------------------------------------------------------------
-- usp_Task_Update
-- Full edit, used by the Admin task form. @@ROWCOUNT is returned so the API can
-- translate "nothing updated" into a 404.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Task_Update', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Task_Update;
GO
CREATE PROCEDURE dbo.usp_Task_Update
    @TaskId      INT,
    @Title       NVARCHAR(200),
    @Description NVARCHAR(1000) = NULL,
    @AssignedTo  INT            = NULL,
    @Priority    NVARCHAR(20),
    @Status      NVARCHAR(20),
    @DueDate     DATETIME       = NULL,
    @RowsAffected INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @AssignedTo IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.Users WHERE UserId = @AssignedTo AND IsActive = 1)
    BEGIN
        RAISERROR ('The selected assignee does not exist or is inactive.', 16, 1);
        RETURN;
    END

    UPDATE  dbo.Tasks
    SET     Title       = @Title,
            Description = @Description,
            AssignedTo  = @AssignedTo,
            Priority    = @Priority,
            Status      = @Status,
            DueDate     = @DueDate,
            UpdatedAt   = GETDATE()
    WHERE   TaskId = @TaskId;

    SET @RowsAffected = @@ROWCOUNT;
END
GO

-- ---------------------------------------------------------------------------
-- usp_Task_UpdateStatus
-- The narrow update an Employee is allowed to perform on their own task.
-- @AssignedTo, when supplied, scopes the update so an Employee cannot change
-- the status of somebody else's task even by guessing a TaskId.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Task_UpdateStatus', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Task_UpdateStatus;
GO
CREATE PROCEDURE dbo.usp_Task_UpdateStatus
    @TaskId       INT,
    @Status       NVARCHAR(20),
    @AssignedTo   INT = NULL,
    @RowsAffected INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE  dbo.Tasks
    SET     Status    = @Status,
            UpdatedAt = GETDATE()
    WHERE   TaskId = @TaskId
      AND   (@AssignedTo IS NULL OR AssignedTo = @AssignedTo);

    SET @RowsAffected = @@ROWCOUNT;
END
GO

-- ---------------------------------------------------------------------------
-- usp_Task_Delete
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Task_Delete', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Task_Delete;
GO
CREATE PROCEDURE dbo.usp_Task_Delete
    @TaskId       INT,
    @RowsAffected INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Tasks
    WHERE TaskId = @TaskId;

    SET @RowsAffected = @@ROWCOUNT;
END
GO


/*
================================================================================
  SECTION 4 - STORED PROCEDURES: DASHBOARD
================================================================================
*/

-- ---------------------------------------------------------------------------
-- usp_Dashboard_GetStats
-- The four summary cards, computed in a single pass over the table.
-- Pass @AssignedTo to scope the figures to one Employee; pass NULL for the
-- organisation-wide totals an Admin sees.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Dashboard_GetStats', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Dashboard_GetStats;
GO
CREATE PROCEDURE dbo.usp_Dashboard_GetStats
    @AssignedTo INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        TotalTasks        = COUNT(*),
        PendingTasks      = SUM(CASE WHEN t.Status = 'Pending'    THEN 1 ELSE 0 END),
        InProgressTasks   = SUM(CASE WHEN t.Status = 'InProgress' THEN 1 ELSE 0 END),
        CompletedTasks    = SUM(CASE WHEN t.Status = 'Completed'  THEN 1 ELSE 0 END),
        HighPriorityTasks = SUM(CASE WHEN t.Priority = 'High'     THEN 1 ELSE 0 END),
        OverdueTasks      = SUM(CASE WHEN t.DueDate IS NOT NULL
                                      AND t.DueDate < GETDATE()
                                      AND t.Status <> 'Completed' THEN 1 ELSE 0 END)
    FROM dbo.Tasks AS t
    WHERE (@AssignedTo IS NULL OR t.AssignedTo = @AssignedTo)
    OPTION (RECOMPILE);
END
GO

-- ---------------------------------------------------------------------------
-- usp_Dashboard_GetRecentTasks
-- The "Recent tasks" widget. TOP is used rather than OFFSET/FETCH to stay
-- compatible with SQL Server 2008.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('dbo.usp_Dashboard_GetRecentTasks', 'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_Dashboard_GetRecentTasks;
GO
CREATE PROCEDURE dbo.usp_Dashboard_GetRecentTasks
    @AssignedTo INT = NULL,
    @TopCount   INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@TopCount)
            t.TaskId,
            t.Title,
            t.Description,
            t.AssignedTo,
            a.Name AS AssignedToName,
            t.Priority,
            t.Status,
            t.DueDate,
            t.CreatedBy,
            c.Name AS CreatedByName,
            t.CreatedAt,
            t.UpdatedAt
    FROM    dbo.Tasks AS t
            LEFT JOIN dbo.Users AS a ON a.UserId = t.AssignedTo
            LEFT JOIN dbo.Users AS c ON c.UserId = t.CreatedBy
    WHERE   (@AssignedTo IS NULL OR t.AssignedTo = @AssignedTo)
    ORDER BY t.CreatedAt DESC, t.TaskId DESC
    OPTION (RECOMPILE);
END
GO


/*
================================================================================
  SECTION 5 - SEED DATA
================================================================================
  Demo accounts. The PasswordHash values below are PBKDF2-SHA256 hashes in the
  format  iterations.base64(salt).base64(hash)  produced by the same hasher the
  API uses to verify them (see PasswordHasher.cs).

  Demo password for every seeded account:  Password@123

  Change or remove these before using the system with real data.
================================================================================
*/

IF NOT EXISTS (SELECT 1 FROM dbo.Users)
BEGIN
    INSERT INTO dbo.Users (Name, Email, PasswordHash, Role)
    VALUES
        (N'System Administrator', N'admin@tasktracker.com',
            N'100000.zbD9MIteDhqWL23oBg0Ucg==.5m1o5HK3DlhiPh0ENj8+abiSzndo+JICNNggxpw0kHk=', N'Admin'),
        (N'Arun Kumar',           N'arun@tasktracker.com',
            N'100000.M3E5Nlg7ZwHl2tTO5YIy7Q==.bwzC08dyLnnTE+Pm4HsESf0wLn1XKbvneX2YjNcIG5E=', N'Employee'),
        (N'Divya Ramesh',         N'divya@tasktracker.com',
            N'100000.q6b3Rj2UdsP9vf1/C9Tr+w==.u6lN/LdCcAPnxwftPBdmI6S65AtaUKFM+4BF3JLJ4mA=', N'Employee'),
        (N'Karthik Selvam',       N'karthik@tasktracker.com',
            N'100000.v5sgElH33p5lKYD89CL16w==.fMxwDb8IsLlqRJXnHQ5jt9YYIShpT8QBb5y9n37dxJ8=', N'Employee'),
        (N'Akil',                 N'akilprabhu2004@gmail.com',
            N'100000.CGC6dY3k4TFIhScu8H8iWw==.ceF3P57LDZyngh2WNsjn6Oc8fk5X3Tmr+d1UZuYJHdg=', N'Employee');

    PRINT 'Seeded dbo.Users with 1 Admin and 4 Employee accounts.';
END
GO

-- ---------------------------------------------------------------------------
-- Added after the first release, so it is inserted separately: the seed block
-- above only runs when the table is empty, and would be skipped on a database
-- that already exists.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'akilprabhu2004@gmail.com')
BEGIN
    INSERT INTO dbo.Users (Name, Email, PasswordHash, Role)
    VALUES (N'Akil', N'akilprabhu2004@gmail.com',
            N'100000.CGC6dY3k4TFIhScu8H8iWw==.ceF3P57LDZyngh2WNsjn6Oc8fk5X3Tmr+d1UZuYJHdg=', N'Employee');

    PRINT 'Added employee Akil.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Tasks)
BEGIN
    DECLARE @AdminId   INT = (SELECT UserId FROM dbo.Users WHERE Email = N'admin@tasktracker.com');
    DECLARE @ArunId    INT = (SELECT UserId FROM dbo.Users WHERE Email = N'arun@tasktracker.com');
    DECLARE @DivyaId   INT = (SELECT UserId FROM dbo.Users WHERE Email = N'divya@tasktracker.com');
    DECLARE @KarthikId INT = (SELECT UserId FROM dbo.Users WHERE Email = N'karthik@tasktracker.com');

    INSERT INTO dbo.Tasks (Title, Description, AssignedTo, Priority, Status, DueDate, CreatedBy)
    VALUES
        (N'Prepare monthly production report',
         N'Consolidate the mill output figures for the month and circulate the summary to the plant heads.',
         @ArunId,    N'High',   N'Pending',    DATEADD(DAY,  3, GETDATE()), @AdminId),

        (N'Review vendor invoices',
         N'Cross-check the pending vendor invoices against purchase orders before the payment run.',
         @DivyaId,   N'Medium', N'InProgress', DATEADD(DAY,  5, GETDATE()), @AdminId),

        (N'Update employee handbook',
         N'Incorporate the revised leave policy and reissue the handbook to all departments.',
         @KarthikId, N'Low',    N'Pending',    DATEADD(DAY, 14, GETDATE()), @AdminId),

        (N'Migrate legacy attendance data',
         N'Move the archived attendance records into the new system and validate the row counts.',
         @ArunId,    N'High',   N'InProgress', DATEADD(DAY,  7, GETDATE()), @AdminId),

        (N'Quarterly safety audit',
         N'Complete the shop-floor safety walkthrough and file the compliance checklist.',
         @DivyaId,   N'High',   N'Completed',  DATEADD(DAY, -2, GETDATE()), @AdminId),

        (N'Renew software licences',
         N'Confirm seat counts with each department and renew the licences before expiry.',
         @KarthikId, N'Medium', N'Completed',  DATEADD(DAY, -8, GETDATE()), @AdminId),

        (N'Fix overtime calculation defect',
         N'Overtime is rounding down on shifts that cross midnight. Reproduce, patch and regression-test.',
         @ArunId,    N'High',   N'Pending',    DATEADD(DAY, -1, GETDATE()), @AdminId),

        (N'Onboard new trainees',
         N'Create system accounts and schedule the induction sessions for the incoming trainee batch.',
         NULL,       N'Low',    N'Pending',    DATEADD(DAY, 10, GETDATE()), @AdminId);

    PRINT 'Seeded dbo.Tasks with 8 sample tasks.';
END
GO

PRINT '';
PRINT '================================================================';
PRINT ' Employee Task Tracker database setup completed successfully.';
PRINT '';
PRINT ' Demo accounts (password for all: Password@123)';
PRINT '   admin@tasktracker.com     - Admin';
PRINT '   arun@tasktracker.com      - Employee';
PRINT '   divya@tasktracker.com     - Employee';
PRINT '   karthik@tasktracker.com   - Employee';
PRINT '   akilprabhu2004@gmail.com  - Employee';
PRINT '================================================================';
GO

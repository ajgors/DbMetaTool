# Firebird Database Metadata Script Generator

A .NET 8.0 console application for managing Firebird 5.0 database metadata through script generation and execution.

## Overview

This tool provides three main operations for managing Firebird 5.0 databases:

1. **Build Database** - Create a new database from SQL scripts
2. **Export Scripts** - Extract database metadata and generate scripts
3. **Update Database** - Apply differential updates to existing databases

Supported objects are limited to:
- Domains
- Tables (with columns)
- Procedures (with parameters)

## Prerequisites

- .NET 8.0 SDK
- Firebird 5.0 server/embedded installed
- Connection access to Firebird database

## Configuration
Before running the application, you must create an appsettings.json file in the root of the project containing your default Firebird server credentials and connection details:

example `appsettings.json`:
```JSON
{
  "DatabaseCredentials": {
    "Username": "SYSDBA",
    "Password": "masterkey",
    "DataSource": "localhost",
    "Port": 3050
  }
}
```

## Installation

1. Clone the repository
2. Navigate to the project directory
3. Build the project:
   ```powershell
   dotnet build
   ```

## Usage

### Build Database from Scripts

Creates a new Firebird database and executes scripts from a directory.

```
dotnet run -- build-db --db-dir <path_to_database> --scripts-dir <path_to_scripts>
```

**Parameters:**
- `--db-dir`: Directory where the new database file (.fdb) will be created
- `--scripts-dir`: Directory containing .sql script files

### Export Scripts from Database

Extracts metadata from an existing database and generates script files.

```
dotnet run -- export-scripts --connection-string <connection_string>  --output-dir <output_directory>
```
example connection string:
```
"User=SYSDBA;Password=masterkey;Database=C:\path\to\db.fdb;DataSource=localhost;Port=3050;"
```

**Parameters:**
- `--connection-string`: Firebird connection string to existing database
- `--output-dir`: Directory where generated scripts will be saved

**Output Format:**
By default, generates SQL scripts:
- `01_domains.sql` - Domain definitions
- `02_tables.sql` - Table definitions with columns
- `03_procedures.sql` - Procedure source code

### Update Database

Applies scripts to an existing database, comparing with current metadata to apply only new/modified objects.

```
dotnet run -- update-db --connection-string <connection_string> --scripts-dir <path_to_scripts>
```

**Parameters:**
- `--connection-string`: Firebird connection string
- `--scripts-dir`: Directory containing scripts to apply



## Test Database

```SQL
/*******************************************************************************
 1. DOMAINS
*******************************************************************************/

CREATE DOMAIN DM_STATUS AS VARCHAR(20) 
    DEFAULT 'ACTIVE' 
    CHECK (VALUE IN ('ACTIVE', 'INACTIVE', 'SUSPENDED'));

CREATE DOMAIN DM_MONEY AS NUMERIC(10, 2) 
    DEFAULT 0.00 
    NOT NULL;


/*******************************************************************************
 2. TABLES
*******************************************************************************/

CREATE TABLE DEPARTMENTS (
    ID INTEGER NOT NULL PRIMARY KEY,
    NAME VARCHAR(100) NOT NULL,
    
    CONSTRAINT UNQ_DEPT_NAME UNIQUE (NAME),
    CONSTRAINT CHK_DEPT_NAME CHECK (TRIM(NAME) <> '')
);

CREATE TABLE EMPLOYEES (
    ID INTEGER NOT NULL PRIMARY KEY,
    DEPARTMENT_ID INTEGER NOT NULL,
    FIRST_NAME VARCHAR(50) NOT NULL,
    LAST_NAME VARCHAR(50) NOT NULL,
    SALARY DM_MONEY,
    STATUS DM_STATUS,
    
    CONSTRAINT FK_EMPLOYEE_DEPT FOREIGN KEY (DEPARTMENT_ID) 
        REFERENCES DEPARTMENTS(ID) ON DELETE CASCADE,
    CONSTRAINT UNQ_EMP_FULLNAME UNIQUE (FIRST_NAME, LAST_NAME),
    CONSTRAINT CHK_EMP_SALARY CHECK (SALARY >= 0),
    CONSTRAINT CHK_EMP_NAME CHECK (TRIM(FIRST_NAME) <> '' AND TRIM(LAST_NAME) <> '')
);


/*******************************************************************************
 3. PROCEDURES
*******************************************************************************/

SET TERM ^ ;

CREATE OR ALTER PROCEDURE SP_ADD_EMPLOYEE (
    P_ID INTEGER,
    P_DEPARTMENT_ID INTEGER,
    P_FIRST_NAME VARCHAR(50),
    P_LAST_NAME VARCHAR(50),
    P_SALARY DM_MONEY
)
AS
BEGIN
    INSERT INTO EMPLOYEES (ID, DEPARTMENT_ID, FIRST_NAME, LAST_NAME, SALARY)
    VALUES (:P_ID, :P_DEPARTMENT_ID, :P_FIRST_NAME, :P_LAST_NAME, :P_SALARY);
END^


CREATE OR ALTER PROCEDURE SP_GET_DEPARTMENT_STATS (
    P_DEPARTMENT_ID INTEGER
)
RETURNS (
    O_DEPARTMENT_NAME VARCHAR(100),
    O_EMPLOYEE_COUNT INTEGER,
    O_TOTAL_SALARY DM_MONEY,
    O_AVERAGE_SALARY DM_MONEY
)
AS
BEGIN
    SELECT NAME 
    FROM DEPARTMENTS 
    WHERE ID = :P_DEPARTMENT_ID
    INTO :O_DEPARTMENT_NAME;

    SELECT 
        COUNT(ID),
        COALESCE(SUM(SALARY), 0),
        COALESCE(AVG(SALARY), 0)
    FROM EMPLOYEES
    WHERE DEPARTMENT_ID = :P_DEPARTMENT_ID AND STATUS = 'ACTIVE'
    INTO :O_EMPLOYEE_COUNT, :O_TOTAL_SALARY, :O_AVERAGE_SALARY;

    SUSPEND;
END^

SET TERM ; ^
```

Export Scripts option generates the following files from the test database:

- `01_domains.sql`:
```SQL
-- Firebird Domains Script

CREATE DOMAIN DM_MONEY AS NUMERIC(10,2)
	DEFAULT 0.00
	NOT NULL
;
CREATE DOMAIN DM_STATUS AS VARCHAR(20)
	DEFAULT 'ACTIVE'
	CHECK (VALUE IN ('ACTIVE', 'INACTIVE', 'SUSPENDED'))
;
```

- `02_tables.sql`:
```SQL
-- Firebird Tables Script

CREATE TABLE DEPARTMENTS (
  ID INTEGER NOT NULL,
  NAME VARCHAR(100) NOT NULL,
  CONSTRAINT PK_DEPARTMENTS PRIMARY KEY (ID),
  CONSTRAINT UNQ_DEPARTMENTS_NAME UNIQUE (NAME),
  CONSTRAINT CHK_DEPT_NAME CHECK (TRIM(NAME) <> '')
)
;

CREATE TABLE EMPLOYEES (
  ID INTEGER NOT NULL,
  DEPARTMENT_ID INTEGER NOT NULL,
  FIRST_NAME VARCHAR(50) NOT NULL,
  LAST_NAME VARCHAR(50) NOT NULL,
  SALARY DM_MONEY,
  STATUS DM_STATUS,
  CONSTRAINT PK_EMPLOYEES PRIMARY KEY (ID),
  CONSTRAINT UNQ_EMPLOYEES_FIRST_NAME UNIQUE (FIRST_NAME),
  CONSTRAINT UNQ_EMPLOYEES_LAST_NAME UNIQUE (LAST_NAME),
  CONSTRAINT CHK_EMP_NAME CHECK (TRIM(FIRST_NAME) <> '' AND TRIM(LAST_NAME) <> ''),
  CONSTRAINT CHK_EMP_SALARY CHECK (SALARY >= 0),
  CONSTRAINT FK_EMPLOYEE_DEPT FOREIGN KEY (DEPARTMENT_ID) REFERENCES DEPARTMENTS(ID) ON DELETE CASCADE
)
;
```

- `03_procedures.sql`:
```SQL
-- Firebird Procedures Script

CREATE OR ALTER PROCEDURE SP_ADD_EMPLOYEE (
  P_ID INTEGER,
  P_DEPARTMENT_ID INTEGER,
  P_FIRST_NAME VARCHAR(50),
  P_LAST_NAME VARCHAR(50),
  P_SALARY NUMERIC(10, 2)
)
AS
BEGIN
    INSERT INTO EMPLOYEES (ID, DEPARTMENT_ID, FIRST_NAME, LAST_NAME, SALARY)
    VALUES (:P_ID, :P_DEPARTMENT_ID, :P_FIRST_NAME, :P_LAST_NAME, :P_SALARY);
END
;

CREATE OR ALTER PROCEDURE SP_GET_DEPARTMENT_STATS (
  P_DEPARTMENT_ID INTEGER
)

RETURNS (
  O_DEPARTMENT_NAME VARCHAR(100),
  O_EMPLOYEE_COUNT INTEGER,
  O_TOTAL_SALARY NUMERIC(10, 2),
  O_AVERAGE_SALARY NUMERIC(10, 2)
)
AS
BEGIN
    SELECT NAME 
    FROM DEPARTMENTS 
    WHERE ID = :P_DEPARTMENT_ID
    INTO :O_DEPARTMENT_NAME;

    SELECT 
        COUNT(ID),
        COALESCE(SUM(SALARY), 0),
        COALESCE(AVG(SALARY), 0)
    FROM EMPLOYEES
    WHERE DEPARTMENT_ID = :P_DEPARTMENT_ID AND STATUS = 'ACTIVE'
    INTO :O_EMPLOYEE_COUNT, :O_TOTAL_SALARY, :O_AVERAGE_SALARY;

    SUSPEND;
END
;
```

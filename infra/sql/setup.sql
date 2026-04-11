-- Idempotent SQL setup — run after every Bicep deployment
-- Requires: SQL Server Entra admin configured + SQL Server system-assigned identity enabled

-- Function App managed identity: runtime data access
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(FunctionAppName)')
    CREATE USER [$(FunctionAppName)] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [$(FunctionAppName)];
ALTER ROLE db_datawriter ADD MEMBER [$(FunctionAppName)];
ALTER ROLE db_ddladmin ADD MEMBER [$(FunctionAppName)];

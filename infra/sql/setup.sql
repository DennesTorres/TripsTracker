-- Idempotent SQL setup — run after every Bicep deployment
-- Requires: SQL Server Entra admin configured + SQL Server system-assigned identity enabled
-- Uses WITH OBJECT_ID syntax — does NOT require Directory Readers on the SQL server identity

-- Function App managed identity: runtime data access
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(FunctionAppName)')
    CREATE USER [$(FunctionAppName)] WITH OBJECT_ID = '$(FunctionAppPrincipalId)';
ALTER ROLE db_datareader ADD MEMBER [$(FunctionAppName)];
ALTER ROLE db_datawriter ADD MEMBER [$(FunctionAppName)];
ALTER ROLE db_ddladmin ADD MEMBER [$(FunctionAppName)];

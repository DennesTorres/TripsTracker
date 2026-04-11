-- Idempotent SQL setup — run after every Bicep deployment
-- Uses WITH OBJECT_ID to bypass SQL Server Directory Readers requirement
-- (FROM EXTERNAL PROVIDER requires server identity + Directory Readers role)

-- Function App managed identity: runtime data access
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(FunctionAppName)')
    CREATE USER [$(FunctionAppName)] WITH OBJECT_ID = '$(FunctionAppPrincipalId)';
ALTER ROLE db_datareader ADD MEMBER [$(FunctionAppName)];
ALTER ROLE db_datawriter ADD MEMBER [$(FunctionAppName)];
ALTER ROLE db_ddladmin ADD MEMBER [$(FunctionAppName)];

-- Idempotent SQL setup — run after every Bicep deployment
-- Grants access to the Functions managed identity and restores SQLAdmins group access

-- SQLAdmins group: retain database management access
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'SQLAdmins')
    CREATE USER [SQLAdmins] FROM EXTERNAL PROVIDER;
ALTER ROLE db_owner ADD MEMBER [SQLAdmins];

-- Functions managed identity: runtime access
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$(FunctionAppName)')
    CREATE USER [$(FunctionAppName)] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [$(FunctionAppName)];
ALTER ROLE db_datawriter ADD MEMBER [$(FunctionAppName)];
ALTER ROLE db_ddladmin ADD MEMBER [$(FunctionAppName)];

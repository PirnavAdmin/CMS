IF DB_ID(N'$(DbName)') IS NULL
BEGIN
    DECLARE @createDb nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(N'$(DbName)');
    EXEC(@createDb);
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'$(DbUser)')
BEGIN
    CREATE LOGIN [$(DbUser)] WITH PASSWORD = '$(DbPassword)', CHECK_POLICY = ON;
END;
GO
USE [$(DbName)];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'$(DbUser)')
BEGIN
    CREATE USER [$(DbUser)] FOR LOGIN [$(DbUser)];
    ALTER ROLE db_owner ADD MEMBER [$(DbUser)];
END;
GO

-- Data corrections — 2026-03-19
-- Run once against the TripsTracker database after Seed_InitialData.sql

-- Fix 1: Guarapari had Vitória's coordinates (-40.34, -20.32).
--         Guarapari is ~40 km south of Vitória.
UPDATE [dbo].[Places]
SET    Lon = -40.508, Lat = -20.671
WHERE  Id = 100 AND City = N'Guarapari';

-- Fix 2: Piraí (MG) and Miguel Pereira (MG) are duplicates of the RJ entries
--         with identical coordinates.  The user visited the RJ cities, not MG.
UPDATE [dbo].[Places] SET IsDeleted = 1 WHERE Id = 97;  -- Miguel Pereira (MG) duplicate
UPDATE [dbo].[Places] SET IsDeleted = 1 WHERE Id = 98;  -- Piraí (MG) duplicate

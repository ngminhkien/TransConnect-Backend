-- Read-only SQL Server preflight for the 20261019 foundation migrations.
-- Run against the intended development/test database before starting the new Host.
-- Empty result sets mean no conflicts were found by these checks; no records are repaired.
SELECT m.AccountId, m.Id AS MemberId, m.OrganizationId, d.Id AS DriverId
FROM [operator].Members m JOIN [operator].Drivers d ON d.AccountId = m.AccountId
WHERE m.Role = N'OrganizationOwner';

SELECT OrganizationId, COUNT(*) AS ActiveOwnerCount
FROM [operator].Members
WHERE Role = N'OrganizationOwner' AND IsActive = 1 AND DeletedAt IS NULL
GROUP BY OrganizationId HAVING COUNT(*) > 1;

SELECT Id, AccountGroup FROM [identity].Users
WHERE AccountGroup NOT IN ('Customer','Operator','InternalAdmin');
SELECT Id, Role FROM [operator].Members
WHERE Role NOT IN ('OrganizationOwner','Manager','Dispatcher');
SELECT Id, Role FROM [operator].Invitations
WHERE Role NOT IN ('OrganizationOwner','Manager','Dispatcher','Driver');
SELECT Id, ReviewStatus FROM [operator].Organizations
WHERE ReviewStatus NOT IN ('Pending','Approved','Rejected');
SELECT Id, ReviewStatus FROM [operator].Drivers
WHERE ReviewStatus NOT IN ('Pending','Approved','Rejected');
SELECT Id, Status FROM [notification].EmailMessages WHERE Status NOT IN (0,1,2,3);

IF EXISTS (SELECT 1 FROM [operator].Members m JOIN [operator].Drivers d ON d.AccountId=m.AccountId WHERE m.Role='OrganizationOwner')
    OR EXISTS (SELECT OrganizationId FROM [operator].Members WHERE Role='OrganizationOwner' AND IsActive=1 AND DeletedAt IS NULL GROUP BY OrganizationId HAVING COUNT(*)>1)
    OR EXISTS (SELECT 1 FROM [identity].Users WHERE AccountGroup NOT IN ('Customer','Operator','InternalAdmin'))
    OR EXISTS (SELECT 1 FROM [operator].Members WHERE Role NOT IN ('OrganizationOwner','Manager','Dispatcher'))
    OR EXISTS (SELECT 1 FROM [operator].Invitations WHERE Role NOT IN ('OrganizationOwner','Manager','Dispatcher','Driver'))
    OR EXISTS (SELECT 1 FROM [operator].Organizations WHERE ReviewStatus NOT IN ('Pending','Approved','Rejected'))
    OR EXISTS (SELECT 1 FROM [operator].Drivers WHERE ReviewStatus NOT IN ('Pending','Approved','Rejected'))
    OR EXISTS (SELECT 1 FROM [notification].EmailMessages WHERE Status NOT IN (0,1,2,3))
    THROW 51007, 'Foundation preflight found conflicts. No data was repaired; resolve the reported records.', 1;

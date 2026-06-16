USE [NatureKnit]
GO

/* Register the newer pages in the live permission registry.
   Pages are defined as '<PageKey>.View/.Edit/.Delete' rows in
   [identity].[Permissions] (the old dbo.AppPages table is gone).
   Safe to re-run: only inserts names that don't exist yet.       */

CREATE TABLE #NewPerms (Name NVARCHAR(100), Description NVARCHAR(255));
INSERT INTO #NewPerms (Name, Description) VALUES
('ForMasterPlaning.View',   'Permission to View Master Planning (Knitter Assignment)'),
('ForMasterPlaning.Edit',   'Permission to Edit Master Planning (Knitter Assignment)'),
('ForMasterPlaning.Delete', 'Permission to Delete in Master Planning (Knitter Assignment)'),
('PlaningReport.View',      'Permission to View the Planing Report (CEO)'),
('PlaningReport.Edit',      'Permission to Edit the Planing Report (CEO)'),
('PlaningReport.Delete',    'Permission to Delete in the Planing Report (CEO)'),
('KnitGanttChart.View',     'Permission to View the Knit Gantt Chart'),
('KnitGanttChart.Edit',     'Permission to Edit the Knit Gantt Chart'),
('KnitGanttChart.Delete',   'Permission to Delete in the Knit Gantt Chart');

INSERT INTO [identity].[Permissions] (Id, Name, Description)
SELECT NEWID(), t.Name, t.Description
FROM #NewPerms t
LEFT JOIN [identity].[Permissions] p ON t.Name = p.Name
WHERE p.Id IS NULL;

DROP TABLE #NewPerms;
PRINT 'New pages registered in identity.Permissions.';
GO

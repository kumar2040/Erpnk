CREATE OR ALTER PROCEDURE dbo.sp_ApproveYarnOrder
    @YoId             INT,
    @Approve          BIT = 1,
    @Action           VARCHAR(20) = NULL, -- 'APPROVE', 'REJECT', 'NOTIFY'
    @Note             NVARCHAR(400) = NULL,
    @UserId           NVARCHAR(450) = NULL,
    @AssigneeUserIds  NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Act VARCHAR(20) = UPPER(ISNULL(@Action, CASE WHEN @Approve = 1 THEN 'APPROVE' ELSE 'REJECT' END));
    DECLARE @YoNo VARCHAR(30),
            @CreatedBy VARCHAR(50),
            @CurrentStatus VARCHAR(30),
            @PoTaskId INT,
            @TaskFromStatus CHAR(1);
    DECLARE @TargetAssignees TABLE
    (
        [UserId] NVARCHAR(450) NOT NULL PRIMARY KEY
    );

    INSERT INTO @TargetAssignees ([UserId])
    SELECT DISTINCT CONVERT(NVARCHAR(450), LTRIM(RTRIM([value])))
    FROM STRING_SPLIT(ISNULL(@AssigneeUserIds, N''), N'|')
    WHERE LEN(LTRIM(RTRIM([value]))) BETWEEN 1 AND 450;

    SELECT @YoNo = [yo_no],
           @CreatedBy = [created_by],
           @CurrentStatus = [status]
    FROM dbo.[tbl_yarn_order]
    WHERE [yo_id] = @YoId;

    IF @YoNo IS NULL
    BEGIN
        SELECT CAST(0 AS BIT) AS [IsSuccess], N'Yarn order not found.' AS [Message];
        RETURN;
    END;

    IF @Act NOT IN ('APPROVE', 'REJECT', 'NOTIFY')
    BEGIN
        SELECT CAST(0 AS BIT) AS [IsSuccess], N'Unsupported approval action.' AS [Message];
        RETURN;
    END;

    IF @Act IN ('APPROVE', 'NOTIFY') AND NOT EXISTS (SELECT 1 FROM @TargetAssignees)
    BEGIN
        SELECT CAST(0 AS BIT) AS [IsSuccess],
               CASE WHEN @Act = 'NOTIFY'
                    THEN N'No YarnControl users are assigned.'
                    ELSE N'No Yarn users are assigned.'
               END AS [Message];
        RETURN;
    END;

    IF @Act = 'NOTIFY' AND ISNULL(@CurrentStatus, '') NOT IN ('Ready for Approval', 'Placed')
    BEGIN
        SELECT CAST(0 AS BIT) AS [IsSuccess], N'Only a ready yarn request can be sent for approval.' AS [Message];
        RETURN;
    END;

    IF @Act IN ('APPROVE', 'REJECT') AND ISNULL(@CurrentStatus, '') <> 'Pending Approval'
    BEGIN
        SELECT CAST(0 AS BIT) AS [IsSuccess], N'Only a pending yarn request can be approved or rejected.' AS [Message];
        RETURN;
    END;

    SELECT TOP (1) @PoTaskId = [PoTaskId],
                   @TaskFromStatus = [Status]
    FROM dbo.[PoTask]
    WHERE [Stage] = 12
      AND [RefId] = @YoId
      AND [IsActive] = 1
    ORDER BY [PoTaskId] DESC;

    BEGIN TRANSACTION;

    IF @Act = 'NOTIFY'
    BEGIN
        UPDATE dbo.[tbl_yarn_order]
        SET [status] = 'Pending Approval'
        WHERE [yo_id] = @YoId;

        IF @PoTaskId IS NOT NULL
        BEGIN
            UPDATE dbo.[PoTaskAssignee]
            SET [IsActive] = 0
            WHERE [PoTaskId] = @PoTaskId
              AND [IsActive] = 1;

            UPDATE dbo.[PoTaskAssignee]
            SET [Status] = 'S',
                [StartDate] = NULL,
                [CompletedDate] = NULL,
                [Note] = NULL,
                [AssignedBy] = ISNULL(@UserId, 'system'),
                [AssignedDate] = GETDATE(),
                [IsActive] = 1
            WHERE [PoTaskId] = @PoTaskId
              AND [UserId] IN (SELECT [UserId] FROM @TargetAssignees);

            INSERT INTO dbo.[PoTaskAssignee]
                ([PoTaskId], [UserId], [Status], [AssignedBy])
            SELECT @PoTaskId, t.[UserId], 'S', ISNULL(@UserId, 'system')
            FROM @TargetAssignees t
            WHERE NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.[PoTaskAssignee] a
                      WHERE a.[PoTaskId] = @PoTaskId
                        AND a.[UserId] = t.[UserId]
                  );

            UPDATE dbo.[PoTask]
            SET [Status] = 'S',
                [Title] = N'Approve yarn order - ' + @YoNo,
                [Detail] = N'Yarn order ' + @YoNo + N' is awaiting YarnControl approval.',
                [ModifiedBy] = ISNULL(@UserId, 'system'),
                [ModifiedDate] = GETDATE()
            WHERE [PoTaskId] = @PoTaskId;

            INSERT INTO dbo.[PoTaskNotification]
                ([UserId], [PoTaskId], [Kind], [Title], [Body])
            SELECT t.[UserId], @PoTaskId, 'U', N'Yarn order awaiting approval',
                   N'Yarn order ' + @YoNo + N' requires YarnControl approval.'
            FROM @TargetAssignees t;
        END;

        COMMIT TRANSACTION;
        SELECT CAST(1 AS BIT) AS [IsSuccess],
               N'Yarn order ' + @YoNo + N' sent to YarnControl for approval.' AS [Message];
        RETURN;
    END;

    IF @Act = 'APPROVE'
    BEGIN
        UPDATE dbo.[tbl_yarn_order]
        SET [status] = 'Approved'
        WHERE [yo_id] = @YoId;

        IF @PoTaskId IS NOT NULL
        BEGIN
            UPDATE dbo.[PoTaskAssignee]
            SET [IsActive] = 0
            WHERE [PoTaskId] = @PoTaskId
              AND [IsActive] = 1;

            UPDATE dbo.[PoTaskAssignee]
            SET [Status] = 'P',
                [StartDate] = ISNULL([StartDate], GETDATE()),
                [CompletedDate] = NULL,
                [Note] = NULL,
                [AssignedBy] = ISNULL(@UserId, 'system'),
                [AssignedDate] = GETDATE(),
                [IsActive] = 1
            WHERE [PoTaskId] = @PoTaskId
              AND [UserId] IN (SELECT [UserId] FROM @TargetAssignees);

            INSERT INTO dbo.[PoTaskAssignee]
                ([PoTaskId], [UserId], [Status], [StartDate], [AssignedBy])
            SELECT @PoTaskId, t.[UserId], 'P', GETDATE(), ISNULL(@UserId, 'system')
            FROM @TargetAssignees t
            WHERE NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.[PoTaskAssignee] a
                      WHERE a.[PoTaskId] = @PoTaskId
                        AND a.[UserId] = t.[UserId]
                  );

            UPDATE dbo.[PoTask]
            SET [Status] = 'P',
                [Title] = N'Place vendor yarn order - ' + @YoNo,
                [Detail] = N'Yarn order ' + @YoNo + N' was approved. Place the vendor order.',
                [ModifiedBy] = ISNULL(@UserId, 'system'),
                [ModifiedDate] = GETDATE()
            WHERE [PoTaskId] = @PoTaskId;

            INSERT INTO dbo.[PoTaskHistory]
                ([PoTaskId], [FromStatus], [ToStatus], [Note], [ChangedBy])
            VALUES
                (@PoTaskId, ISNULL(@TaskFromStatus, 'S'), 'P',
                 ISNULL(@Note, N'Approved by YarnControl - place vendor order.'),
                 ISNULL(@UserId, 'system'));

            INSERT INTO dbo.[PoTaskNotification]
                ([UserId], [PoTaskId], [Kind], [Title], [Body])
            SELECT t.[UserId], @PoTaskId, 'A', N'Yarn order approved',
                   N'Yarn order ' + @YoNo + N' was approved. Place the vendor order.'
            FROM @TargetAssignees t;
        END;

        IF @CreatedBy IS NOT NULL
        BEGIN
            INSERT INTO dbo.[PoTaskNotification]
                ([UserId], [PoTaskId], [Kind], [Title], [Body])
            VALUES
                (@CreatedBy, @PoTaskId, 'A', N'Yarn order approved',
                 N'Yarn order ' + @YoNo + N' has been approved by YarnControl.');
        END;

        COMMIT TRANSACTION;
        SELECT CAST(1 AS BIT) AS [IsSuccess],
               N'Yarn order ' + @YoNo + N' approved successfully.' AS [Message];
        RETURN;
    END;

    UPDATE dbo.[tbl_yarn_order]
    SET [status] = 'Rejected'
    WHERE [yo_id] = @YoId;

    IF @PoTaskId IS NOT NULL
    BEGIN
        UPDATE dbo.[PoTask]
        SET [Status] = 'X',
            [ModifiedBy] = ISNULL(@UserId, 'system'),
            [ModifiedDate] = GETDATE()
        WHERE [PoTaskId] = @PoTaskId;

        UPDATE dbo.[PoTaskAssignee]
        SET [Status] = 'X',
            [CompletedDate] = GETDATE()
        WHERE [PoTaskId] = @PoTaskId
          AND [IsActive] = 1;

        INSERT INTO dbo.[PoTaskHistory]
            ([PoTaskId], [FromStatus], [ToStatus], [Note], [ChangedBy])
        VALUES
            (@PoTaskId, ISNULL(@TaskFromStatus, 'S'), 'X',
             ISNULL(@Note, N'Rejected by YarnControl'),
             ISNULL(@UserId, 'system'));
    END;

    IF @CreatedBy IS NOT NULL
    BEGIN
        INSERT INTO dbo.[PoTaskNotification]
            ([UserId], [PoTaskId], [Kind], [Title], [Body])
        VALUES
            (@CreatedBy, @PoTaskId, 'R', N'Yarn order rejected',
             N'Yarn order ' + @YoNo + N' was rejected by YarnControl.'
             + ISNULL(N' Note: ' + @Note, N''));
    END;

    COMMIT TRANSACTION;
    SELECT CAST(1 AS BIT) AS [IsSuccess],
           N'Yarn order ' + @YoNo + N' has been rejected.' AS [Message];
END;

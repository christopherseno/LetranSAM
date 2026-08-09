USE [LetranIntegratedSystem]
GO
-- =============================================
-- Author:      Christopher Seno
-- Create date: 8-04-2026
-- Description: Persistence for the Debit/Credit Memo & Adjustments posting workflow
--              (draft -> final), mirroring dbo.DeferredIncome / dbo.DeferredIncomeFee
--              but kept in the [AR] schema alongside [AR].[ArTrailMemoDetail].
--
--              One header row per (PeriodId, PostingDate) batch, plus detail rows at the
--              grain returned by [AR].[ArTrailMemoDetail]: (MemoType, AcaDept, Fee).
--              Amount    = full memo/adjustment amount as of the posting date.
--              PostedAmount = amount recognized for NthMonth of NoOfMonths
--                             (Amount / NoOfMonths * NthMonth minus prior finalized months).
-- =============================================

IF SCHEMA_ID('AR') IS NULL
    EXEC('CREATE SCHEMA [AR]');
GO

IF OBJECT_ID('AR.MemoAdjustmentPostingDetail', 'U') IS NULL
BEGIN
    IF OBJECT_ID('AR.MemoAdjustmentPosting', 'U') IS NULL
    BEGIN
        CREATE TABLE AR.MemoAdjustmentPosting
        (
            Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MemoAdjustmentPosting PRIMARY KEY,
            PeriodId      int      NOT NULL,
            PostingDate   date     NOT NULL,
            DateGenerated datetime NOT NULL,
            GeneratedBy   int      NOT NULL,
            IsFinal       bit      NOT NULL CONSTRAINT DF_MemoAdjustmentPosting_IsFinal DEFAULT (0),
            NthMonth      int      NOT NULL,
            NoOfMonths    int      NOT NULL
        );

        CREATE UNIQUE INDEX UX_MemoAdjustmentPosting_Period_Date
            ON AR.MemoAdjustmentPosting (PeriodId, PostingDate);
    END

    CREATE TABLE AR.MemoAdjustmentPostingDetail
    (
        Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MemoAdjustmentPostingDetail PRIMARY KEY,
        PostingId      int           NOT NULL
            CONSTRAINT FK_MemoAdjustmentPostingDetail_Posting
            REFERENCES AR.MemoAdjustmentPosting (Id),
        MemoType       varchar(20)   NOT NULL,      -- DebitMemo | CreditMemo | DNForm | CMForm
        AcaDeptId      int           NOT NULL,
        FeeId          int           NULL,
        Particular     nvarchar(255) NULL,
        ChartOfAccount nvarchar(255) NULL,
        QNECode        nvarchar(100) NULL,
        Amount         decimal(18,4) NOT NULL,
        PostedAmount   decimal(18,4) NOT NULL
    );

    CREATE INDEX IX_MemoAdjustmentPostingDetail_Posting
        ON AR.MemoAdjustmentPostingDetail (PostingId);
END
GO

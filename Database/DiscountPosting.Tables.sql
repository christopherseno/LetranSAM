USE [LetranIntegratedSystem]
GO
-- =============================================
-- Author:      Christopher Seno
-- Create date: 8-04-2026
-- Description: Persistence for the Discount posting workflow (draft -> final),
--              mirroring AR.MemoAdjustmentPosting / AR.MemoAdjustmentPostingDetail.
--
--              One header row per (PeriodId, PostingDate) batch, plus detail rows at the
--              grain returned by [AR].[ArTrailDiscountDetail2024]: (AcaDept, Fee).
--              Amount    = full discount amount as of the posting date.
--              PostedAmount = amount recognized for NthMonth of NoOfMonths
--                             (Amount / NoOfMonths * NthMonth minus prior finalized months).
--              DiscType is kept for parity with the memo tables; it is always 'Discount'.
-- =============================================

IF SCHEMA_ID('AR') IS NULL
    EXEC('CREATE SCHEMA [AR]');
GO

IF OBJECT_ID('AR.DiscountPostingDetail', 'U') IS NULL
BEGIN
    IF OBJECT_ID('AR.DiscountPosting', 'U') IS NULL
    BEGIN
        CREATE TABLE AR.DiscountPosting
        (
            Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiscountPosting PRIMARY KEY,
            PeriodId      int      NOT NULL,
            PostingDate   date     NOT NULL,
            DateGenerated datetime NOT NULL,
            GeneratedBy   int      NOT NULL,
            IsFinal       bit      NOT NULL CONSTRAINT DF_DiscountPosting_IsFinal DEFAULT (0),
            NthMonth      int      NOT NULL,
            NoOfMonths    int      NOT NULL
        );

        CREATE UNIQUE INDEX UX_DiscountPosting_Period_Date
            ON AR.DiscountPosting (PeriodId, PostingDate);
    END

    CREATE TABLE AR.DiscountPostingDetail
    (
        Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DiscountPostingDetail PRIMARY KEY,
        PostingId      int           NOT NULL
            CONSTRAINT FK_DiscountPostingDetail_Posting
            REFERENCES AR.DiscountPosting (Id),
        DiscType       varchar(20)   NOT NULL,      -- always 'Discount' (parity with memo tables)
        AcaDeptId      int           NOT NULL,
        FeeId          int           NULL,
        Particular     nvarchar(255) NULL,
        ChartOfAccount nvarchar(255) NULL,
        QNECode        nvarchar(100) NULL,
        Amount         decimal(18,4) NOT NULL,
        PostedAmount   decimal(18,4) NOT NULL
    );

    CREATE INDEX IX_DiscountPostingDetail_Posting
        ON AR.DiscountPostingDetail (PostingId);
END
GO

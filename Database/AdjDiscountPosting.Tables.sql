USE [LetranIntegratedSystem]
GO
-- =============================================
-- Author:      Christopher Seno
-- Create date: 8-04-2026
-- Description: Persistence for the Adjustment-Discount posting workflow (draft -> final),
--              mirroring AR.DiscountPosting / AR.DiscountPostingDetail.
--
--              One header row per (PeriodId, PostingDate) batch, plus detail rows at the
--              grain returned by [AR].[ArTrailAdjDiscountDetail2024]: (AcaDept, Fee).
--              Amount    = full adjustment-discount amount as of the posting date.
--              PostedAmount = amount recognized for NthMonth of NoOfMonths
--                             (Amount / NoOfMonths * NthMonth minus prior finalized months).
--              DiscType is kept for parity; it is always 'AdjDiscount'.
-- =============================================

IF SCHEMA_ID('AR') IS NULL
    EXEC('CREATE SCHEMA [AR]');
GO

IF OBJECT_ID('AR.AdjDiscountPostingDetail', 'U') IS NULL
BEGIN
    IF OBJECT_ID('AR.AdjDiscountPosting', 'U') IS NULL
    BEGIN
        CREATE TABLE AR.AdjDiscountPosting
        (
            Id            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AdjDiscountPosting PRIMARY KEY,
            PeriodId      int      NOT NULL,
            PostingDate   date     NOT NULL,
            DateGenerated datetime NOT NULL,
            GeneratedBy   int      NOT NULL,
            IsFinal       bit      NOT NULL CONSTRAINT DF_AdjDiscountPosting_IsFinal DEFAULT (0),
            NthMonth      int      NOT NULL,
            NoOfMonths    int      NOT NULL
        );

        CREATE UNIQUE INDEX UX_AdjDiscountPosting_Period_Date
            ON AR.AdjDiscountPosting (PeriodId, PostingDate);
    END

    CREATE TABLE AR.AdjDiscountPostingDetail
    (
        Id             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AdjDiscountPostingDetail PRIMARY KEY,
        PostingId      int           NOT NULL
            CONSTRAINT FK_AdjDiscountPostingDetail_Posting
            REFERENCES AR.AdjDiscountPosting (Id),
        DiscType       varchar(20)   NOT NULL,      -- always 'AdjDiscount' (parity with other posting tables)
        AcaDeptId      int           NOT NULL,
        FeeId          int           NULL,
        Particular     nvarchar(255) NULL,
        ChartOfAccount nvarchar(255) NULL,
        QNECode        nvarchar(100) NULL,
        Amount         decimal(18,4) NOT NULL,
        PostedAmount   decimal(18,4) NOT NULL
    );

    CREATE INDEX IX_AdjDiscountPostingDetail_Posting
        ON AR.AdjDiscountPostingDetail (PostingId);
END
GO

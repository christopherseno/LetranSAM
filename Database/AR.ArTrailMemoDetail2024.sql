USE [LetranIntegratedSystem]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:      Christopher Seno
-- Create date: 7-9-2026
-- Description: Detailed breakdown of the four memo columns produced by
--              AR.ArTrail2024 (DNForm, CMForm, DebitMemo, CreditMemo).
--
--              The result is in "long" format: one row per
--              (MemoType, Academic Department, Fee/Account) with the summed
--              Amount, plus the mapped Chart of Account and QNE code where they
--              exist.
--
--              Grain per source:
--                * DNForm / CMForm  -> adjustments, broken down BY FEE using
--                                      AdjustmentDetailFees. Each adjustment is
--                                      classified as a whole (DM if its net fee
--                                      movement > 0, CM if < 0), exactly like the
--                                      parent trail, so the detail reconciles to
--                                      the DNForm / CMForm totals.
--                * DebitMemo / CreditMemo -> DMCM rows. DMCM carries no per-fee
--                                      amount (DmcmDiscountDetail has no amount),
--                                      so these summarize BY CHART OF ACCOUNT
--                                      (AcctID); FeeID is left NULL.
-- =============================================
CREATE OR ALTER PROCEDURE [AR].[ArTrailMemoDetail2024]
    @periodid int,
    @asofdate date
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @pid int = @periodid;
    DECLARE @edatekey date = @asofdate;

    ;WITH
    -- ---- Classify each adjustment as a DM Form or CM Form (same rule as AR.ArTrail2024) ----
    -- Net fee movement = (Tuition + Aircon + Labo + Other) with the refund rate
    -- applied on Action = 0 (no-effect / refund) rows. > 0 => DM Form, < 0 => CM Form.
    AdjClass AS (
        SELECT  e.AdjustmentID,
                cur.AcaDeptID,
                CASE WHEN SUM( (ISNULL(f.TuitionFee,0) + ISNULL(f.AirconFee,0)
                              + ISNULL(f.LaboFee,0) + ISNULL(f.OtherFee,0))
                              * IIF(f.Action = 0, ISNULL(f.RefundRate,1), 1) ) > 0
                     THEN 'DNForm' ELSE 'CMForm' END AS MemoType
        FROM    Adjustment e
        JOIN    AdjustmentDetails f ON f.AdjustmentID = e.AdjustmentID
        JOIN    Student_Section ss  ON ss.Student_SectionID = e.StudentSectionID
        JOIN    Section sec         ON sec.SectionID = ss.SectionID
        JOIN    Curriculum cur      ON cur.CurriculumID = sec.CurriculumID
        WHERE   sec.PeriodID = @pid
          AND   e.ValidationDate IS NOT NULL
          AND   CAST(e.ValidationDate AS date) <= @edatekey
        GROUP BY e.AdjustmentID, cur.AcaDeptID
        HAVING  SUM( (ISNULL(f.TuitionFee,0) + ISNULL(f.AirconFee,0)
                     + ISNULL(f.LaboFee,0) + ISNULL(f.OtherFee,0))
                     * IIF(f.Action = 0, ISNULL(f.RefundRate,1), 1) ) <> 0
    ),
    -- ---- Per-fee amounts for those adjustments (refund rate applied to match the parent) ----
    FormFees AS (
        SELECT  ac.MemoType,
                ac.AcaDeptID,
                adf.FeeID,
                SUM( ISNULL(adf.Amount,0) * IIF(fd.Action = 0, ISNULL(fd.RefundRate,1), 1) ) AS Amount
        FROM    AdjClass ac
        JOIN    AdjustmentDetails fd     ON fd.AdjustmentID = ac.AdjustmentID
        JOIN    AdjustmentDetailFees adf ON adf.AdjustmentDetailsID = fd.AdjustmentDetailsID
        GROUP BY ac.MemoType, ac.AcaDeptID, adf.FeeID
    ),
    -- ---- Debit / Credit memos (DMCM), account level ----
    MemoAccts AS (
        SELECT  CASE WHEN d.DC = 'D' THEN 'DebitMemo' ELSE 'CreditMemo' END AS MemoType,
                d.AcaDeptID,
                d.AcctID,
                SUM(ROUND(ISNULL(d.Amount,0),2)) AS Amount
        FROM    DMCM d
        WHERE   d.PeriodID = @pid
          AND   d.ChargeToStudentAr = 1
          AND   d.DC IN ('D','C')
          AND   CAST(d.TransactionDate AS date) <= @edatekey
        GROUP BY CASE WHEN d.DC = 'D' THEN 'DebitMemo' ELSE 'CreditMemo' END,
                 d.AcaDeptID, d.AcctID
    )

    -- ================= Unified detail =================
    SELECT  @pid                       AS PeriodID,
            x.MemoType,
            x.AcaDeptID,
            dept.AcaAcronym,
            dept.AcaDepartmentName,
            x.FeeID,
            x.Particular,
            x.ChartOfAccount,
            x.QNECode,
            SUM(x.Amount)              AS Amount
    FROM (
        -- DM / CM Forms, by Fee
        SELECT  ff.MemoType,
                ff.AcaDeptID,
                ff.FeeID,
                ISNULL(fn.FeeName1, 'Fee ' + CAST(ff.FeeID AS varchar(12))) AS Particular,
                CASE WHEN coa.AcctID IS NULL THEN NULL
                     ELSE coa.AcctNo + ' - ' + coa.AcctName END             AS ChartOfAccount,
                COALESCE(NULLIF(fee.QneAccountCode, ''),
                         NULLIF(coa.QNEAccountCode, ''))                     AS QNECode,
                ff.Amount
        FROM    FormFees ff
        LEFT JOIN Fee fee             ON fee.FeeID = ff.FeeID
        LEFT JOIN FeeName fn          ON fn.FeeNameID = fee.FeeNameID
        LEFT JOIN ChartOfAccounts coa ON coa.AcctID = fee.AcctID

        UNION ALL

        -- Debit / Credit Memos, by Chart of Account
        SELECT  ma.MemoType,
                ma.AcaDeptID,
                NULL                                                        AS FeeID,
                ISNULL(coa.AcctName, 'Account ' + CAST(ma.AcctID AS varchar(12))) AS Particular,
                CASE WHEN coa.AcctID IS NULL THEN NULL
                     ELSE coa.AcctNo + ' - ' + coa.AcctName END             AS ChartOfAccount,
                NULLIF(coa.QNEAccountCode, '')                              AS QNECode,
                ma.Amount
        FROM    MemoAccts ma
        LEFT JOIN ChartOfAccounts coa ON coa.AcctID = ma.AcctID
    ) x
    LEFT JOIN AcademicDepartment dept ON dept.AcaDeptID = x.AcaDeptID
    GROUP BY x.MemoType, x.AcaDeptID, dept.AcaAcronym, dept.AcaDepartmentName,
             x.FeeID, x.Particular, x.ChartOfAccount, x.QNECode
    ORDER BY x.MemoType, dept.AcaAcronym, x.Particular;
END
GO

USE [LetranIntegratedSystem]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:      Christopher Seno
-- Create date: 7-30-2026
-- Description: Detailed breakdown of the "AdjDiscount" column produced by
--              AR.ArTrail2024 (the discount portion carried by adjustments,
--              i.e. e.adjDiscount + f.adjDiscount in the parent).
--
--              The result uses the SAME column list as AR.ArTrailMemoDetail /
--              AR.ArTrailDiscountDetail: one row per (MemoType, Academic
--              Department, Fee) with the summed Amount, plus the mapped Chart of
--              Account and QNE code. MemoType is always 'AdjDiscount'.
--
--              Grain / reconciliation:
--                In the parent, AdjDiscount for an adjustment detail is:
--                    AdjDiscount  * TuitionFee  (Tuition category)
--                  + AdjDiscountA * AirconFee   (Aircon  category)
--                  + AdjDiscountL * LaboFee     (Labo    category)
--                  + AdjDiscountO * OtherFee    (Other   category)
--                each * the refund rate when Action = 0. The parent sums this
--                over BOTH the DM-Form set (net fee movement > 0) and the
--                CM-Form set (net fee movement < 0) -- i.e. every adjustment
--                whose net fee movement <> 0 -- so this proc uses exactly that
--                set (AdjSet) and unpivots the four category discounts, mapping:
--                    Tuition -> the period's tuition fee (FeeCategory 'T')
--                    Aircon  -> the period's aircon fee  (via Aircon)
--                    Labo    -> AdjustmentDetails.LabFeeID   (the specific fee)
--                    Other   -> AdjustmentDetails.OtherFeeID (the specific fee)
--                purely to resolve Particular / Chart of Account / QNE code.
--                Summed across all rows the detail reconciles to the parent
--                AdjDiscount total.
-- =============================================
CREATE OR ALTER PROCEDURE [AR].[ArTrailAdjDiscountDetail2024]
    @periodid int,
    @asofdate date
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @pid int = @periodid;
    DECLARE @edatekey date = @asofdate;

    -- Representative fees for the Tuition and Aircon categories (these two carry
    -- no per-detail FeeID, only a category amount + rate), used to resolve the
    -- Chart of Account / QNE columns for those categories.
    DECLARE @tuitionFeeID int = (SELECT TOP (1) FeeID FROM Fee
                                 WHERE PeriodID = @pid AND FeeCategory = 'T' ORDER BY FeeID);
    DECLARE @airconFeeID  int = (SELECT TOP (1) a.FeeID FROM Fee a
                                 JOIN Aircon d ON d.FeeID = a.FeeID
                                 WHERE a.PeriodID = @pid ORDER BY a.FeeID);

    WITH
    -- ---- Adjustments that move fees (net fee movement <> 0), same rule as the ----
    -- ---- union of the DM-Form (> 0) and CM-Form (< 0) sets in AR.ArTrail2024. ----
    AdjSet AS (
        SELECT  e.AdjustmentID,
                cur.AcaDeptID
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
    -- ---- Per-detail discount, unpivoted into its four fee categories ----
    -- Discount = rate * category fee * refund factor (refund applied on Action = 0),
    -- exactly the terms summed by e.adjDiscount / f.adjDiscount in the parent.
    AdjDiscFees AS (
        SELECT  aset.AcaDeptID,
                cat.FeeID,
                cat.DiscAmount
        FROM    AdjSet aset
        JOIN    AdjustmentDetails fd ON fd.AdjustmentID = aset.AdjustmentID
        CROSS APPLY (VALUES
            (@tuitionFeeID, ISNULL(fd.AdjDiscount ,0) * ISNULL(fd.TuitionFee,0) * IIF(fd.Action = 0, ISNULL(fd.RefundRate,1), 1)),
            (@airconFeeID , ISNULL(fd.AdjDiscountA,0) * ISNULL(fd.AirconFee ,0) * IIF(fd.Action = 0, ISNULL(fd.RefundRate,1), 1)),
            (fd.LabFeeID  , ISNULL(fd.AdjDiscountL,0) * ISNULL(fd.LaboFee   ,0) * IIF(fd.Action = 0, ISNULL(fd.RefundRate,1), 1)),
            (fd.OtherFeeID, ISNULL(fd.AdjDiscountO,0) * ISNULL(fd.OtherFee  ,0) * IIF(fd.Action = 0, ISNULL(fd.RefundRate,1), 1))
        ) cat(FeeID, DiscAmount)
        WHERE cat.DiscAmount <> 0
    ),
    -- ---- Fee -> Description / Chart of Account (same resolution used by the other detail procs) ----
    FeeInfo AS (
        SELECT  a.FeeID,
                a.FeeCategory,
                CASE WHEN d.FeeID IS NOT NULL THEN 'Aircon'
                     ELSE ISNULL(g.Description,
                          ISNULL(f.Description,
                          ISNULL(e.Description,
                          ISNULL(b.Description,
                          ISNULL(c.Description, 'Tuition'))))) END AS Description,
                a.AcctID
        FROM    Fee a
        LEFT JOIN Supplemental  b ON b.FeeID = a.FeeID
        LEFT JOIN Miscellaneous c ON c.FeeID = a.FeeID
        LEFT JOIN Aircon        d ON d.FeeID = a.FeeID
        LEFT JOIN Lab           e ON e.FeeID = a.FeeID
        LEFT JOIN Others        f ON f.FeeID = a.FeeID
        LEFT JOIN Various       g ON g.FeeID = a.FeeID
        WHERE   a.PeriodID = @pid
    )

    -- ================= Unified detail (same column list as AR.ArTrailMemoDetail) =================
    SELECT  @pid                                                        AS PeriodID,
            'AdjDiscount'                                               AS MemoType,
            adf.AcaDeptID,
            dept.AcaAcronym,
            dept.AcaDepartmentName,
            adf.FeeID,
            ISNULL(fi.Description, 'Tuition')                           AS Particular,
            CASE WHEN coa.AcctID IS NULL THEN NULL
                 ELSE coa.AcctNo + ' - ' + coa.AcctName END            AS ChartOfAccount,
            NULLIF(coa.QNEAccountCode, '')                             AS QNECode,
            SUM(adf.DiscAmount)                                        AS Amount
    FROM    AdjDiscFees adf
    LEFT JOIN FeeInfo fi              ON fi.FeeID   = adf.FeeID
    LEFT JOIN ChartOfAccounts coa     ON coa.AcctID = fi.AcctID
    LEFT JOIN AcademicDepartment dept ON dept.AcaDeptID = adf.AcaDeptID
    GROUP BY adf.AcaDeptID, dept.AcaAcronym, dept.AcaDepartmentName,
             adf.FeeID, ISNULL(fi.Description, 'Tuition'),
             CASE WHEN coa.AcctID IS NULL THEN NULL
                  ELSE coa.AcctNo + ' - ' + coa.AcctName END,
             NULLIF(coa.QNEAccountCode, '')
    HAVING  SUM(adf.DiscAmount) <> 0
    ORDER BY dept.AcaAcronym, Particular;
END
GO

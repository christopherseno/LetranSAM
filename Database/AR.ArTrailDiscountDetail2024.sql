USE [LetranIntegratedSystem]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:      Christopher Seno
-- Create date: 7-30-2026
-- Description: Detailed breakdown of the "Discount" column produced by
--              AR.ArTrail2024.
--
--              The result uses the SAME column list as AR.ArTrailMemoDetail:
--              one row per (MemoType, Academic Department, Fee) with the summed
--              Amount, plus the mapped Chart of Account and QNE code where they
--              exist. MemoType is always 'Discount'.
--
--              Grain / reconciliation:
--                The parent trail's Discount is:
--                    SUM( CASE WHEN the student has a Discount that is NOT a
--                              voucher-type (DiscountTypeID = 155) THEN the
--                              Assessment.DiscountAmount
--                         ELSE 0 END )
--                summed over every validated Student_Section in the period.
--                Because Assessment carries DiscountAmount and FeeID directly,
--                this detail simply keeps that same CASE but groups BY FEE
--                (and Academic Department) instead of collapsing to the section,
--                so the detail reconciles back to the parent Discount total.
--
--                The LEFT JOIN to Discount is preserved exactly as in the parent
--                (a student with more than one qualifying discount row sums the
--                DiscountAmount once per row) so the numbers match the trail.
-- =============================================
CREATE OR ALTER PROCEDURE [AR].[ArTrailDiscountDetail2024]
    @periodid int,
    @asofdate date
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @pid int = @periodid;
    DECLARE @edatekey date = @asofdate;

    WITH
    -- ---- Discount per (Student_Section, Fee), same CASE rule as AR.ArTrail2024 ----
    -- Voucher-type discounts (DiscountTypeID = 155) and students with no discount
    -- contribute 0, exactly like the parent trail's "disc" subquery.
    DiscFees AS (
        SELECT  cur.AcaDeptID,
                asmt.FeeID,
                SUM( CASE WHEN disc.DiscountID IS NOT NULL AND disc.DiscountTypeID = 155 THEN 0
                          WHEN disc.DiscountID IS NULL THEN 0
                          ELSE ISNULL(asmt.DiscountAmount, 0) END ) AS Amount
        FROM    Assessment asmt
        JOIN    Student_Section ss ON ss.Student_SectionID = asmt.Student_SectionID
        JOIN    Section sec        ON sec.SectionID = ss.SectionID AND sec.PeriodID = @pid
        JOIN    Curriculum cur     ON cur.CurriculumID = sec.CurriculumID
        LEFT JOIN Discount disc    ON disc.StudentID = ss.StudentID AND disc.PeriodID = @pid
        WHERE   ss.ValidationDate IS NOT NULL
          AND   ss.ValidationDate <= @edatekey
        GROUP BY cur.AcaDeptID, asmt.FeeID
    ),
    -- ---- Fee -> Description / Chart of Account (same resolution used by AR.ArTrailMemoDetail) ----
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
            'Discount'                                                  AS MemoType,
            df.AcaDeptID,
            dept.AcaAcronym,
            dept.AcaDepartmentName,
            df.FeeID,
            ISNULL(fi.Description, 'Tuition')                           AS Particular,
            CASE WHEN coa.AcctID IS NULL THEN NULL
                 ELSE coa.AcctNo + ' - ' + coa.AcctName END            AS ChartOfAccount,
            NULLIF(coa.QNEAccountCode, '')                             AS QNECode,
            SUM(df.Amount)                                             AS Amount
    FROM    DiscFees df
    LEFT JOIN FeeInfo fi            ON fi.FeeID  = df.FeeID
    LEFT JOIN ChartOfAccounts coa   ON coa.AcctID = fi.AcctID
    LEFT JOIN AcademicDepartment dept ON dept.AcaDeptID = df.AcaDeptID
    GROUP BY df.AcaDeptID, dept.AcaAcronym, dept.AcaDepartmentName,
             df.FeeID, ISNULL(fi.Description, 'Tuition'),
             CASE WHEN coa.AcctID IS NULL THEN NULL
                  ELSE coa.AcctNo + ' - ' + coa.AcctName END,
             NULLIF(coa.QNEAccountCode, '')
    HAVING  SUM(df.Amount) <> 0
    ORDER BY dept.AcaAcronym, Particular;
END
GO

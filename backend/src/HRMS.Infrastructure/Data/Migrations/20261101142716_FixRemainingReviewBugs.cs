using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixRemainingReviewBugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "MandatorySince",
                table: "TrainingPrograms",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HourlyRate",
                table: "TimesheetLines",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CarryForwardOverridden",
                table: "LeaveBalances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PreviousJoiningDate",
                table: "JobChanges",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PreviousLeavingDate",
                table: "JobChanges",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByEmployeeId",
                table: "JobApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VerdictGiven",
                table: "InterviewFeedback",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LetterSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Last = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LetterSequences", x => x.Id);
                });

            // Data written before this migration. Several facts were only kept in the audit trail (entity ids there are lower-case GUIDs).

            // Re-hires: the earlier employment period was only kept in the note ("Previously worked 5 Mar 2025 – 10 Jun 2025.").
            // Dates that don't parse stay empty, as for an unknown leaving date.
            migrationBuilder.Sql(@"
UPDATE j SET
    PreviousJoiningDate = TRY_CONVERT(date, NULLIF(SUBSTRING(j.Note, 19, CASE WHEN x.Dash > 19 THEN x.Dash - 19 ELSE 0 END), N''), 106),
    PreviousLeavingDate = TRY_CONVERT(date, NULLIF(SUBSTRING(j.Note, x.Dash + 3, CASE WHEN y.Dot > x.Dash + 3 THEN y.Dot - x.Dash - 3 ELSE 0 END), N''), 106)
FROM JobChanges j
CROSS APPLY (SELECT CHARINDEX(N' – ', j.Note) AS Dash) x
CROSS APPLY (SELECT CHARINDEX(N'.', j.Note, x.Dash + 3) AS Dot) y
WHERE j.Kind = 6 AND j.Note LIKE N'Previously worked % – %.%';");

            // A carry-forward HR has ever typed in (the opening balances page was its only editor) is kept from now on.
            migrationBuilder.Sql(@"
UPDATE b SET CarryForwardOverridden = 1
FROM LeaveBalances b
JOIN (SELECT DISTINCT EntityId FROM AuditLogs
      WHERE EntityType = N'LeaveBalance' AND Action = N'Updated' AND Changes LIKE N'%""CarriedForward""%') a
  ON a.EntityId = LOWER(CONVERT(nvarchar(36), b.Id));");

            // Hours already logged keep the person's current rate, so removing them from the team no longer drops the cost.
            migrationBuilder.Sql(@"
UPDATE l SET HourlyRate = m.HourlyRate
FROM TimesheetLines l
JOIN Timesheets t ON t.Id = l.TimesheetId
JOIN ProjectMembers m ON m.ProjectId = l.ProjectId AND m.EmployeeId = t.EmployeeId
WHERE m.HourlyRate IS NOT NULL;");

            // Interview rows count as a verdict unless they were made by saving scorecard marks and never given one:
            // made by the feedback form (no marks then), or later changed by it (text, a verdict other than the default
            // Yes, or a rating change in the audit trail).
            migrationBuilder.Sql(@"
UPDATE InterviewFeedback SET VerdictGiven = 1
WHERE MarksJson IS NULL OR Strengths IS NOT NULL OR Concerns IS NOT NULL OR Recommendation <> 3;
UPDATE f SET VerdictGiven = 1
FROM InterviewFeedback f
JOIN (SELECT DISTINCT EntityId FROM AuditLogs
      WHERE EntityType = N'InterviewFeedback'
        AND ((Action = N'Created' AND Changes LIKE N'%""MarksJson"":{""new"":null}%')
          OR (Action = N'Updated' AND (Changes LIKE N'%""Rating""%' OR Changes LIKE N'%""Recommendation""%')))) a
  ON a.EntityId = LOWER(CONVERT(nvarchar(36), f.Id))
WHERE f.VerdictGiven = 0;");

            // Referrals: the hiring log says ""Referred by <name>""; link it when exactly one employee of the company has that name.
            migrationBuilder.Sql(@"
UPDATE a SET ReferredByEmployeeId = e.Id
FROM JobApplications a
CROSS APPLY (SELECT TOP 1 ev.Text FROM ApplicationEvents ev
             WHERE ev.ApplicationId = a.Id AND ev.Text LIKE N'Referred by %' ORDER BY ev.CreatedAtUtc) t
JOIN (SELECT TenantId, FirstName + N' ' + LastName AS Name, CAST(MAX(CAST(Id AS char(36))) AS uniqueidentifier) AS Id
      FROM Employees GROUP BY TenantId, FirstName + N' ' + LastName HAVING COUNT(*) = 1) e
  ON e.TenantId = a.TenantId AND t.Text = N'Referred by ' + e.Name;");

            // Mandatory courses: the last time the audit trail saw one switched on. Others keep counting from creation.
            migrationBuilder.Sql(@"
UPDATE p SET MandatorySince = CAST(a.Since AS date)
FROM TrainingPrograms p
JOIN (SELECT EntityId, MAX(TimestampUtc) AS Since FROM AuditLogs
      WHERE EntityType = N'TrainingProgram' AND Action = N'Updated' AND Changes LIKE N'%""IsMandatory"":{""old"":false,""new"":true}%'
      GROUP BY EntityId) a
  ON a.EntityId = LOWER(CONVERT(nvarchar(36), p.Id))
WHERE p.IsMandatory = 1;");

            // Letter counters start past every number on file or deleted (from the audit trail), so none is given twice.
            migrationBuilder.Sql(@"
WITH refs AS (
    SELECT TenantId, ReferenceNo FROM IssuedLetters
    UNION ALL SELECT TenantId, ReferenceNo FROM CandidateLetters
    UNION ALL SELECT TenantId, CASE WHEN ISJSON(Changes) = 1 THEN JSON_VALUE(Changes, '$.ReferenceNo.old') END
        FROM AuditLogs WHERE Action = N'Deleted' AND EntityType IN (N'IssuedLetter', N'CandidateLetter') AND TenantId IS NOT NULL
), parsed AS (
    SELECT TenantId, ReferenceNo,
        CASE WHEN PATINDEX(N'%[^0-9]%', REVERSE(ReferenceNo)) > 1 THEN PATINDEX(N'%[^0-9]%', REVERSE(ReferenceNo)) - 1 ELSE 0 END AS Digits
    FROM refs WHERE ReferenceNo IS NOT NULL
)
INSERT INTO LetterSequences (Id, Prefix, Last, CreatedAtUtc, TenantId)
SELECT NEWID(), LEFT(ReferenceNo, LEN(ReferenceNo) - Digits), MAX(TRY_CAST(RIGHT(ReferenceNo, Digits) AS int)), SYSUTCDATETIME(), TenantId
FROM parsed
WHERE Digits > 0 AND LEN(ReferenceNo) - Digits <= 40
GROUP BY TenantId, LEFT(ReferenceNo, LEN(ReferenceNo) - Digits)
HAVING MAX(TRY_CAST(RIGHT(ReferenceNo, Digits) AS int)) IS NOT NULL;");

            // Offer letters could repeat a number before; the earliest keeps it and later copies get the next free ones.
            migrationBuilder.Sql(@"
DECLARE @renumber TABLE (Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier, Prefix nvarchar(40), Number int);
WITH parsed AS (
    SELECT Id, TenantId, ReferenceNo, CreatedAtUtc,
        CASE WHEN PATINDEX(N'%[^0-9]%', REVERSE(ReferenceNo)) > 1 THEN PATINDEX(N'%[^0-9]%', REVERSE(ReferenceNo)) - 1 ELSE 0 END AS Digits
    FROM CandidateLetters
), copies AS (
    SELECT Id, TenantId, CreatedAtUtc, LEFT(ReferenceNo, LEN(ReferenceNo) - Digits) AS Prefix,
        ROW_NUMBER() OVER (PARTITION BY TenantId, ReferenceNo ORDER BY CreatedAtUtc, Id) AS Copy
    FROM parsed
), extra AS (
    SELECT Id, TenantId, Prefix, ROW_NUMBER() OVER (PARTITION BY TenantId, Prefix ORDER BY CreatedAtUtc, Id) AS N
    FROM copies WHERE Copy > 1
)
INSERT INTO @renumber (Id, TenantId, Prefix, Number)
SELECT x.Id, x.TenantId, x.Prefix, ISNULL(s.Last, 0) + x.N
FROM extra x LEFT JOIN LetterSequences s ON s.TenantId = x.TenantId AND s.Prefix = x.Prefix;

UPDATE c SET ReferenceNo = r.Prefix + CASE WHEN r.Number < 10000 THEN RIGHT(N'000' + CAST(r.Number AS nvarchar(10)), 4) ELSE CAST(r.Number AS nvarchar(10)) END
FROM CandidateLetters c JOIN @renumber r ON r.Id = c.Id;

UPDATE s SET Last = m.Number
FROM LetterSequences s
JOIN (SELECT TenantId, Prefix, MAX(Number) AS Number FROM @renumber GROUP BY TenantId, Prefix) m ON m.TenantId = s.TenantId AND m.Prefix = s.Prefix;
INSERT INTO LetterSequences (Id, Prefix, Last, CreatedAtUtc, TenantId)
SELECT NEWID(), m.Prefix, m.Number, SYSUTCDATETIME(), m.TenantId
FROM (SELECT TenantId, Prefix, MAX(Number) AS Number FROM @renumber GROUP BY TenantId, Prefix) m
WHERE NOT EXISTS (SELECT 1 FROM LetterSequences s WHERE s.TenantId = m.TenantId AND s.Prefix = m.Prefix);");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_ReferredByEmployeeId",
                table: "JobApplications",
                column: "ReferredByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateLetters_TenantId_ReferenceNo",
                table: "CandidateLetters",
                columns: new[] { "TenantId", "ReferenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LetterSequences_TenantId_Prefix",
                table: "LetterSequences",
                columns: new[] { "TenantId", "Prefix" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LetterSequences");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_ReferredByEmployeeId",
                table: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_CandidateLetters_TenantId_ReferenceNo",
                table: "CandidateLetters");

            migrationBuilder.DropColumn(
                name: "MandatorySince",
                table: "TrainingPrograms");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "TimesheetLines");

            migrationBuilder.DropColumn(
                name: "CarryForwardOverridden",
                table: "LeaveBalances");

            migrationBuilder.DropColumn(
                name: "PreviousJoiningDate",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "PreviousLeavingDate",
                table: "JobChanges");

            migrationBuilder.DropColumn(
                name: "ReferredByEmployeeId",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "VerdictGiven",
                table: "InterviewFeedback");
        }
    }
}

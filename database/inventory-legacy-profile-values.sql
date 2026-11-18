/*
    Read-only inventory of profile rows that predate the shared profile rules
    (issue #238). Run this before deploying the tightened rules and again after,
    and use the output to decide the correction path for each row.

    This script only reads. It never updates, deletes or coerces a stored value,
    so it is safe to run against any database, including a shared one.

    A row is listed when any of these is true:
      - student_number is not exactly eight digits
      - section is not A-C
      - year_level is not 1-4
      - mobile_number is present and is not eleven digits starting with 09
      - course is neither BSCS nor BSIT, including the legacy long labels

    A pending account request in this list cannot be approved: approval runs
    member creation, which applies the same rules. Reject it and ask the
    applicant to resubmit, or correct the row first.
*/

SET NOCOUNT ON;

DECLARE @eight_digits varchar(20) = '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]';
DECLARE @ph_mobile varchar(30) = '09[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]';

PRINT 'Pending account requests outside the profile rules:';

SELECT
    request_id,
    student_number,
    full_name,
    email,
    course,
    year_level,
    section,
    mobile_number,
    requested_at,
    CASE
        WHEN LEN(student_number) <> 8 OR student_number NOT LIKE @eight_digits THEN 'student_number'
        WHEN section NOT IN ('A', 'B', 'C') THEN 'section'
        WHEN year_level NOT BETWEEN 1 AND 4 THEN 'year_level'
        WHEN mobile_number IS NOT NULL AND LTRIM(RTRIM(mobile_number)) <> ''
             AND (LEN(LTRIM(RTRIM(mobile_number))) <> 11 OR LTRIM(RTRIM(mobile_number)) NOT LIKE @ph_mobile) THEN 'mobile_number'
        WHEN UPPER(LTRIM(RTRIM(course))) NOT IN ('BSCS', 'BSIT', 'BS COMPUTER SCIENCE', 'BS INFORMATION TECHNOLOGY') THEN 'course'
        ELSE 'multiple'
    END AS first_failing_field
FROM account_requests
WHERE account_request_status_id = 1
  AND (
        LEN(student_number) <> 8
     OR student_number NOT LIKE @eight_digits
     OR section NOT IN ('A', 'B', 'C')
     OR year_level NOT BETWEEN 1 AND 4
     OR (mobile_number IS NOT NULL AND LTRIM(RTRIM(mobile_number)) <> ''
         AND (LEN(LTRIM(RTRIM(mobile_number))) <> 11 OR LTRIM(RTRIM(mobile_number)) NOT LIKE @ph_mobile))
     OR UPPER(LTRIM(RTRIM(course))) NOT IN ('BSCS', 'BSIT', 'BS COMPUTER SCIENCE', 'BS INFORMATION TECHNOLOGY')
  )
ORDER BY request_id;

PRINT 'Members outside the profile rules:';

SELECT
    member_id,
    user_id,
    student_number,
    course,
    year_level,
    section,
    mobile_number,
    membership_status_id,
    CASE
        WHEN LEN(student_number) <> 8 OR student_number NOT LIKE @eight_digits THEN 'student_number'
        WHEN section NOT IN ('A', 'B', 'C') THEN 'section'
        WHEN year_level NOT BETWEEN 1 AND 4 THEN 'year_level'
        WHEN mobile_number IS NOT NULL AND LTRIM(RTRIM(mobile_number)) <> ''
             AND (LEN(LTRIM(RTRIM(mobile_number))) <> 11 OR LTRIM(RTRIM(mobile_number)) NOT LIKE @ph_mobile) THEN 'mobile_number'
        WHEN UPPER(LTRIM(RTRIM(course))) NOT IN ('BSCS', 'BSIT', 'BS COMPUTER SCIENCE', 'BS INFORMATION TECHNOLOGY') THEN 'course'
        ELSE 'multiple'
    END AS first_failing_field
FROM members
WHERE LEN(student_number) <> 8
   OR student_number NOT LIKE @eight_digits
   OR section NOT IN ('A', 'B', 'C')
   OR year_level NOT BETWEEN 1 AND 4
   OR (mobile_number IS NOT NULL AND LTRIM(RTRIM(mobile_number)) <> ''
       AND (LEN(LTRIM(RTRIM(mobile_number))) <> 11 OR LTRIM(RTRIM(mobile_number)) NOT LIKE @ph_mobile))
   OR UPPER(LTRIM(RTRIM(course))) NOT IN ('BSCS', 'BSIT', 'BS COMPUTER SCIENCE', 'BS INFORMATION TECHNOLOGY')
ORDER BY member_id;

PRINT 'Counts:';

SELECT
    (SELECT COUNT(*) FROM members) AS members_total,
    (SELECT COUNT(*) FROM account_requests WHERE account_request_status_id = 1) AS pending_requests_total;

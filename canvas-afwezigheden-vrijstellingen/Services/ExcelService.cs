using CanvasAfwezighedenVrijstellingen.Models;
using OfficeOpenXml;

namespace CanvasAfwezighedenVrijstellingen.Services;

public class ExcelService
{
    private static string NormalizeStudentKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // Normalize identifiers coming from Excel/Canvas.
        // - trim
        // - remove whitespace
        // - remove Unicode format chars (e.g., LRM/RLM)
        // - lowercase
        var trimmed = value.Trim();
        var chars = trimmed
            .Where(c => !char.IsWhiteSpace(c) && System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format)
            .ToArray();
        return new string(chars).ToLowerInvariant();
    }

    public ExcelService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public async Task<List<Absence>> ReadAbsencesFromExcel(Stream excelStream)
    {
        var absences = new List<Absence>();

        using var package = new ExcelPackage(excelStream);
        var worksheet = package.Workbook.Worksheets[0];
        
        // Excel format from Artevelde absence export:
        // Row 5 is header row
        // Column A: naam (last name)
        // Column B: voornaam (first name)
        // Column C: e-mail
        // Column D: Begindatum (start date)
        // Column E: Einddatum (end date)
        // Column F: Opmerking (comments/reason)
        // Column G: Status wettiging
        // Column H: Reden
        
        int rowCount = worksheet.Dimension?.Rows ?? 0;
        
        for (int row = 6; row <= rowCount; row++) // Start at 6, data starts after header at row 5
        {
            var lastName = worksheet.Cells[row, 1].Value?.ToString();
            var firstName = worksheet.Cells[row, 2].Value?.ToString();
            var email = worksheet.Cells[row, 3].Value?.ToString();
            var startDateValue = worksheet.Cells[row, 4].Value;
            var endDateValue = worksheet.Cells[row, 5].Value;
            var comments = worksheet.Cells[row, 6].Value?.ToString();
            var status = worksheet.Cells[row, 7].Value?.ToString()?.Trim(); // Column G: Status wettiging
            var reason = worksheet.Cells[row, 8].Value?.ToString(); // Column H: Reden

            // Only process absences with "gewettigd" status
            if (string.IsNullOrWhiteSpace(email) || startDateValue == null)
                continue;
            
            // Check if status contains "gewettigd" (case-insensitive, trimmed)
            if (string.IsNullOrWhiteSpace(status) || !status.Contains("gewettigd", StringComparison.OrdinalIgnoreCase))
                continue;

            DateTime startDate;
            if (startDateValue is DateTime dt)
            {
                startDate = dt;
            }
            else if (DateTime.TryParse(startDateValue.ToString(), out var parsed))
            {
                startDate = parsed;
            }
            else
            {
                continue;
            }

            DateTime? endDate = null;
            if (endDateValue != null)
            {
                if (endDateValue is DateTime dtEnd)
                {
                    endDate = dtEnd;
                }
                else if (DateTime.TryParse(endDateValue.ToString(), out var parsedEnd))
                {
                    endDate = parsedEnd;
                }
            }

            // Extract student ID from email (everything before @)
            var studentId = NormalizeStudentKey(email.Split('@')[0]);
            var studentName = $"{firstName} {lastName}".Trim();

            // Add absence for start date
            absences.Add(new Absence
            {
                StudentId = studentId,
                StudentName = studentName,
                AbsenceDate = startDate.Date,
                Reason = reason ?? comments ?? string.Empty
            });

            // If there's an end date and it's different from start date, add absences for each day
            if (endDate.HasValue && endDate.Value.Date > startDate.Date)
            {
                for (var date = startDate.Date.AddDays(1); date <= endDate.Value.Date; date = date.AddDays(1))
                {
                    absences.Add(new Absence
                    {
                        StudentId = studentId,
                        StudentName = studentName,
                        AbsenceDate = date,
                        Reason = reason ?? comments ?? string.Empty
                    });
                }
            }
        }

        return absences;
    }

    public async Task<List<Attendance>> ReadAttendanceFromExcel(Stream excelStream)
    {
        var attendanceList = new List<Attendance>();

        using var package = new ExcelPackage(excelStream);
        var worksheet = package.Workbook.Worksheets[0];

        // Some exports have an incorrect worksheet dimension; compute a safe used range.
        const int maxRowsToScan = 2000;
        const int maxColsToScan = 50;
        int lastRow = 0;
        int lastCol = 0;
        int lastNonEmptyRow = 0;

        for (int row = 1; row <= maxRowsToScan; row++)
        {
            bool rowHasValue = false;
            for (int col = 1; col <= maxColsToScan; col++)
            {
                var v = worksheet.Cells[row, col].Value;
                if (v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                {
                    rowHasValue = true;
                    lastRow = Math.Max(lastRow, row);
                    lastCol = Math.Max(lastCol, col);
                }
            }

            if (rowHasValue)
            {
                lastNonEmptyRow = row;
            }
            else if (lastNonEmptyRow > 0 && row - lastNonEmptyRow > 50)
            {
                break;
            }
        }

        if (lastRow < 2 || lastCol < 2)
            return attendanceList;

        // Detect Microsoft Forms-style "present list" exports (columns like "Begintijd" and "E-mail").
        int headerRow = -1;
        int emailCol = -1;
        int beginCol = -1;
        int nameCol = -1;

        for (int row = 1; row <= Math.Min(20, lastRow); row++)
        {
            for (int col = 1; col <= lastCol; col++)
            {
                var text = worksheet.Cells[row, col].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (string.Equals(text, "E-mail", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Email", StringComparison.OrdinalIgnoreCase))
                {
                    headerRow = row;
                    emailCol = col;
                }
                else if (string.Equals(text, "Begintijd", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Start time", StringComparison.OrdinalIgnoreCase))
                {
                    beginCol = col;
                }
                else if (string.Equals(text, "Naam", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Name", StringComparison.OrdinalIgnoreCase))
                {
                    nameCol = col;
                }
            }

            if (headerRow != -1 && emailCol != -1 && beginCol != -1)
                break;
        }

        if (headerRow != -1 && emailCol != -1 && beginCol != -1)
        {
            for (int row = headerRow + 1; row <= lastRow; row++)
            {
                var email = worksheet.Cells[row, emailCol].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                    continue;

                var startValue = worksheet.Cells[row, beginCol].Value;
                DateTime start;

                if (startValue is DateTime dt)
                {
                    start = dt;
                }
                else if (startValue is double oa)
                {
                    start = DateTime.FromOADate(oa);
                }
                else if (startValue != null && DateTime.TryParse(startValue.ToString(), out var parsed))
                {
                    start = parsed;
                }
                else
                {
                    continue;
                }

                var studentId = email.Split('@')[0];
                var studentName = nameCol != -1
                    ? (worksheet.Cells[row, nameCol].Value?.ToString() ?? string.Empty).Trim()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(studentName))
                    studentName = studentId;

                attendanceList.Add(new Attendance
                {
                    StudentId = NormalizeStudentKey(studentId),
                    StudentName = studentName,
                    Date = start.Date,
                    Present = true
                });
            }

            return attendanceList;
        }

        // Fallback to matrix-style attendance exports:
        // - First row contains dates
        // - First column contains student names/IDs
        // - Cells contain attendance status (e.g., "Aanwezig", "Afwezig", etc.)

        var dates = new Dictionary<int, DateTime>();
        for (int col = 2; col <= lastCol; col++)
        {
            var dateValue = worksheet.Cells[1, col].Value;
            if (dateValue == null)
                continue;

            DateTime date;
            if (dateValue is DateTime dtd)
            {
                date = dtd;
            }
            else if (dateValue is double oa)
            {
                date = DateTime.FromOADate(oa);
            }
            else if (DateTime.TryParse(dateValue.ToString(), out var parsed))
            {
                date = parsed;
            }
            else
            {
                continue;
            }

            dates[col] = date;
        }

        for (int row = 2; row <= lastRow; row++)
        {
            var studentInfo = worksheet.Cells[row, 1].Value?.ToString();
            if (string.IsNullOrWhiteSpace(studentInfo))
                continue;

            string studentId = string.Empty;
            string studentName = studentInfo;

            if (studentInfo.Contains("(") && studentInfo.Contains("@"))
            {
                var emailStart = studentInfo.IndexOf("(");
                var emailEnd = studentInfo.IndexOf(")");
                if (emailStart >= 0 && emailEnd > emailStart)
                {
                    var email = studentInfo.Substring(emailStart + 1, emailEnd - emailStart - 1);
                    studentId = NormalizeStudentKey(email.Split('@')[0]);
                    studentName = studentInfo.Substring(0, emailStart).Trim();
                }
            }

            foreach (var dateCol in dates)
            {
                var attendanceValue = worksheet.Cells[row, dateCol.Key].Value?.ToString()?.Trim();

                bool isPresent = !string.IsNullOrWhiteSpace(attendanceValue) &&
                                 (attendanceValue.Contains("Aanwezig", StringComparison.OrdinalIgnoreCase) ||
                                  attendanceValue.Contains("Present", StringComparison.OrdinalIgnoreCase));

                attendanceList.Add(new Attendance
                {
                    StudentId = studentId,
                    StudentName = studentName,
                    Date = dateCol.Value.Date,
                    Present = isPresent
                });
            }
        }

        return attendanceList;
    }

    public List<UnjustifiedAbsence> FindUnjustifiedAbsences(
        List<Attendance> attendanceRecords, 
        List<Absence> justifiedAbsences)
    {
        var unjustified = new List<UnjustifiedAbsence>();
        
        // Get all students who were absent (not present)
        var absentRecords = attendanceRecords.Where(a => !a.Present).ToList();
        
        foreach (var absent in absentRecords)
        {
            // Check if this absence is justified
            var isJustified = justifiedAbsences.Any(ja => 
                ja.StudentId.Equals(absent.StudentId, StringComparison.OrdinalIgnoreCase) && 
                ja.AbsenceDate.Date == absent.Date.Date);
            
            if (!isJustified)
            {
                unjustified.Add(new UnjustifiedAbsence
                {
                    StudentId = absent.StudentId,
                    StudentName = absent.StudentName,
                    Date = absent.Date
                });
            }
        }
        
        return unjustified;
    }

    public byte[] ExportGradesToBamaFlexFormat(List<BamaFlexGrade> grades)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Grades");
        
        // Add headers
        worksheet.Cells[1, 1].Value = "Studentnummer";
        worksheet.Cells[1, 2].Value = "Naam";
        worksheet.Cells[1, 3].Value = "E-mail";
        worksheet.Cells[1, 4].Value = "Score";
        
        // Style headers
        using (var range = worksheet.Cells[1, 1, 1, 4])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        }
        
        // Add data
        int row = 2;
        foreach (var grade in grades.OrderBy(g => g.StudentId))
        {
            worksheet.Cells[row, 1].Value = grade.StudentId;
            worksheet.Cells[row, 2].Value = grade.StudentName;
            worksheet.Cells[row, 3].Value = grade.Email;
            worksheet.Cells[row, 4].Value = grade.FinalGrade;
            row++;
        }
        
        // Auto-fit columns
        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        
        return package.GetAsByteArray();
    }
}

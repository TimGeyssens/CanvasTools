using CanvasBamaflexExport.Models;
using OfficeOpenXml;

namespace CanvasBamaflexExport.Services;

public class ExcelService
{
    public ExcelService()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
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

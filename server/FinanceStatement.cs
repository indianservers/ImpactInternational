using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using static Clubs.Access;
namespace Clubs;

public record StatementUpload(string Name, string Content);
public static class FinanceStatement
{
    // Import is a separate confirmed operation. Parsing never posts or reconciles funds.
    public static FinanceBankRow[] Parse(StatementUpload file)
    {
        Check(file.Name != null && file.Name.Length <= 200 && file.Content != null && file.Content.Length <= 700000, "Statement file is too large.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(file.Content); }
        catch (FormatException) { throw new ApiError(400, "Invalid statement file encoding."); }
        Check(bytes.Length <= 500000, "Use a statement smaller than 500 KB.");
        List<string[]> rows;
        if (file.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var stream = new MemoryStream(bytes);
                using var zip = new ZipArchive(stream);
                Check(zip.Entries.Sum(e => e.Length) <= 10000000, "Expanded workbook is too large.");
                XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XDocument Read(string name) { var entry=zip.GetEntry(name); Check(entry!=null,"Workbook worksheet is missing."); using var input=entry.Open(); return XDocument.Load(input); }
                var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? [] : Read("xl/sharedStrings.xml").Descendants(ns+"si").Select(s=>string.Concat(s.Descendants(ns+"t").Select(t=>t.Value))).ToArray();
                rows = Read("xl/worksheets/sheet1.xml").Descendants(ns+"row").Take(502).Select(r => {
                    var cells = new string[4];
                    foreach(var c in r.Elements(ns+"c")) {
                        var address=(string?)c.Attribute("r")??"";
                        var column=0; foreach(var ch in address.TakeWhile(char.IsLetter)) column=column*26+(char.ToUpperInvariant(ch)-'A'+1);
                        if(column<1||column>4)continue;
                        Check(!c.Elements(ns+"f").Any(),"Statement cells must contain values, not formulas.");
                        var value=c.Element(ns+"v")?.Value??string.Concat(c.Descendants(ns+"t").Select(t=>t.Value));
                        if((string?)c.Attribute("t")=="s") {Check(int.TryParse(value,out var index)&&index>=0&&index<strings.Length,"Invalid workbook text reference.");value=strings[index];}
                        cells[column-1]=value;
                    }
                    return cells;
                }).ToList();
            }
            catch (InvalidDataException) { throw new ApiError(400,"Invalid XLSX workbook."); }
            catch (System.Xml.XmlException) { throw new ApiError(400,"Invalid workbook XML."); }
        }
        else { Check(file.Name.EndsWith(".csv",StringComparison.OrdinalIgnoreCase),"Choose CSV or XLSX."); rows=Csv(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF')); }
        Check(rows.Count is > 1 and <= 501,"Use a header and 1–500 statement rows.");
        var headers=rows[0].Select(s=>(s??"").Trim().ToLowerInvariant()).ToArray();
        Check(headers.Length>=3&&headers[0]=="date"&&headers[1]=="amount"&&headers[2]=="reference","Columns must be Date, Amount, Reference, and optional Key. Dates must be YYYY-MM-DD.");
        var result=new List<FinanceBankRow>();
        for(var index=1;index<rows.Count;index++) {
            var r=rows[index];
            Check(r.Length>=3,"Missing columns on row "+(index+1));
            Check(DateOnly.TryParseExact(r[0],"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)&&date<=FinanceService.Today,"Invalid date on row "+(index+1)+". Use YYYY-MM-DD text cells.");
            Check(decimal.TryParse(r[1],NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var amount)&&amount!=0,"Invalid amount on row "+(index+1)+". Use unformatted decimal amounts.");
            var reference=(r[2]??"").Trim();Check(reference.Length is >0 and <=150,"Invalid reference on row "+(index+1));
            var key=r.Length>3&&!string.IsNullOrWhiteSpace(r[3])?r[3].Trim():Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(date.ToString("yyyy-MM-dd")+"|"+amount.ToString("G29",CultureInfo.InvariantCulture)+"|"+reference)));
            Check(key.Length<=150,"Import key is too long.");result.Add(new(date,amount,reference,key));
        }
        Check(result.Select(r=>r.Key).Distinct().Count()==result.Count,"Statement contains duplicate row keys. Check duplicates or supply distinct source keys.");
        return result.ToArray();
    }
    static List<string[]> Csv(string text)
    {
        var rows=new List<string[]>();var row=new List<string>();var value=new StringBuilder();bool quoted=false;
        for(int i=0;i<text.Length;i++) {
            char c=text[i];
            if(c=='"') {if(quoted&&i+1<text.Length&&text[i+1]=='"'){value.Append('"');i++;}else quoted=!quoted;}
            else if(c==','&&!quoted){row.Add(value.ToString());value.Clear();}
            else if((c=='\r'||c=='\n')&&!quoted){if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;row.Add(value.ToString());value.Clear();if(row.Any(v=>v.Length>0))rows.Add(row.ToArray());row.Clear();Check(rows.Count<=501,"At most 500 statement rows are allowed.");}
            else value.Append(c);
        }
        Check(!quoted,"Unclosed CSV quote.");row.Add(value.ToString());if(row.Any(v=>v.Length>0))rows.Add(row.ToArray());return rows;
    }
}

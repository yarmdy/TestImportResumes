using System;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

public class ZhilianResumeImporter : ResumeImporter
{
    public override string ResumeSource => "Zhilian";

    public override Task<CanImportResult> CheckCanImport(Stream stream)
    {
        bool ret = findAllStrings(stream, checkAnchors);
        if (!ret)
        {
            return Task.FromResult(CanImportResult.Error(new NotSupportedException("不支持的文件")));
        }
        return Task.FromResult(CanImportResult.Success());
    }

    public override Task<ImportResult> DoImport(Stream stream)
    {
        try
        {
            long pos = readToStringRequire(stream, "<span style='font-size:");
            pos = readToStringRequire(stream, ">");
            pos = readToStringRequire(stream, "<");
            string FullName = lastString;
            pos = readToStringRequire(stream, "<span style='font-size:");
            pos = readToStringRequire(stream, ">");
            pos = readToStringRequire(stream, "<");
            string[] shenfen = lastString.Split(new[] { "&nbsp;" }, StringSplitOptions.RemoveEmptyEntries);
            string Sex = shenfen[0];
            string Birth = shenfen[1];
            string Exp = shenfen[2];
            string Edu = shenfen[3];
            pos = readToStringRequire(stream, "<span style='font-size:");
            pos = readToStringRequire(stream, ">");
            pos = readToStringRequire(stream, "<");
            string[] shenfen2 = lastString.Split('|',StringSplitOptions.TrimEntries);
            string NowCity = shenfen2[0];
            string Hukou = shenfen2[1];
            string WorkStatus = shenfen2[2];
            pos = readToStringRequire(stream, "<v:imagedata src=\"");
            pos = readToStringRequire(stream, "\"");
            string Photo = lastString;
            pos = readToStringRequire(stream, "<tr style='mso-yfti-irow:2");
            pos = readToStringRequire(stream, "<span style='font-size:");
            pos = readToStringRequire(stream, ">");
            pos = readToStringRequire(stream, "手机：");
            pos = readToStringRequire(stream, "<br />");
            string Mobile = lastString;
            pos = readToStringRequire(stream, "<a href=\"mailto:");
            pos = readToStringRequire(stream, "\">");
            string Email = lastString;
            pos = readToStringRequire(stream, "</table>");
            pos = readToStringRequire(stream, "<p class=MsoNormal");
            pos = readToStringRequire(stream, "<span style='font-size:");
            pos = readToStringRequire(stream, ">求职意向<");
            pos = readToStringRequire(stream, "<table class=MsoTableGrid");
            pos = readToStringRequire(stream, "期望工作地区：");
            pos = readToStringRequire(stream, "<span style='font-size:");
            pos = readToStringRequire(stream, ">");
            pos = readToStringRequire(stream, "<");
            string PreferredLocation = lastString.Trim();
            pos = readToStringRequire(stream, "期望月薪：");
            pos = readToStringRequire(stream, "<span lang=EN-US style='font-size:");
            pos = readToStringRequire(stream, ">");
            pos = readToStringRequire(stream, "<");
            string ExpectedSalary = lastString.Trim();
            pos = readToStringRequire(stream, "期望工作性质：");
            pos = readToStringRequire(stream, "<span style='font-size:");
            pos = readToStringRequire(stream, ">");
            pos = readToStringRequire(stream, "<");
            string WorkType = lastString.Trim();
            pos = readToStringRequire(stream, "期望从事职业：");
            pos = readToStringRequire(stream, "<span style='font-size:");
            pos = readToStringRequire(stream, ">");
            pos = readToStringRequire(stream, "<");
            string Goal = lastString.Trim();
            pos = readToStringRequire(stream, "期望从事行业：");
            pos = readToStringRequire(stream, "<span style='font-size:");
            pos = readToStringRequire(stream, ">");
            pos = readToStringRequire(stream, "<");
            string Industry = lastString.Trim();
            pos = readToStringRequire(stream, "自我评价");
            pos = readToStringRequire(stream, "<span style='font-size:");
            pos = readToStringRequire(stream, ">");
            pos = readToStringRequire(stream, "</span>");
            string Pingjia = lastString.Trim().Replace("<br/>","\r\n");
            long start = readToStringRequire(stream, "工作经历</span>");
            long end = readToStringRequire(stream, "项目经历</span>", false);
            stream.Position = start;
            Memory<byte>  tmpData = new Memory<byte>(new byte[end-start]);
            stream.Read(tmpData.Span);
            string gongzuoData = Encoding.UTF8.GetString(tmpData.Span);
            MatchCollection matches = MapTablesReg.Matches(gongzuoData);
            string WorkExperience = string.Join("\r\n",matches.Select(a => string.Join("\r\n", a.Groups.Values.Skip(1).Select((b,i) => {
                string val = b.Value.Trim().Replace("&nbsp;", " ").Replace("<br/>", "\r\n");
                return i switch { 
                    2=> val,
                    3=> val,
                    _=> string.Join("\r\n", val.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.TrimEntries))
                };
            }))));

            start = stream.Position;
            end = readToStringRequire(stream, "教育经历</span>", false);
            stream.Position = start;
            tmpData = new Memory<byte>(new byte[end - start]);
            stream.Read(tmpData.Span);
            string xiangmuData = Encoding.UTF8.GetString(tmpData.Span);
            matches = MapTablesSkillsReg.Matches(xiangmuData);
            string Projects = HttpUtility.UrlEncode(JsonSerializer.Serialize(matches.Select(a => {
                string nameAndTime = a.Groups[1].Value.Trim().Replace("&nbsp;", " ").Replace("<br/>", "\r\n");
                Match match = MapDateName.Match(nameAndTime);
                string date_1 = match.Success ? match.Groups[2].Value : "";
                return new
                {
                    ProjectName = match.Success ? match.Groups[3].Value : "",
                    Date = match.Success ? match.Groups[1].Value : "",
                    Date_1 = date_1=="至今"?"":date_1,
                    Role = "",
                    ProjectDescription = a.Groups[2].Value.Trim().Replace("&nbsp;", " ").Replace("<br/>", "\r\n"),
                    Technologies = "",
                };
            }).ToArray()));

            start = stream.Position;
            end = readToStringRequire(stream, "证书</span>", false);
            stream.Position = start;
            tmpData = new Memory<byte>(new byte[end - start]);
            stream.Read(tmpData.Span);
            string jiaoyuData = Encoding.UTF8.GetString(tmpData.Span);

            string Education = string.Join("\r\n", MapTableOnlyReg.Matches(jiaoyuData).Select(a => string.Join("\r\n", MapSpanReg.Matches(a.Value).Select((b,i) => {
                string val = MapHtmlReg.Replace(b.Groups[1].Value, "").Trim().Replace("&nbsp;", " ").Replace("<br/>", "\r\n");
                return string.Join("\r\n", val.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.TrimEntries));
            }))));

            return Task.FromResult(ImportResult.Success(ResumeSource,new Dictionary<string,string>()));
        }catch(ZhilianFileReadFail zlex)
        {
            return Task.FromResult(ImportResult.Error(ResumeSource, zlex));
        }catch(Exception ex)
        {
            return Task.FromResult(ImportResult.Error(ResumeSource, ex));
        }
    }
    private long readToStringRequire(Stream stream, string str, bool posToSearchEnd = true)
    {
        long index = readToString(stream, str, posToSearchEnd);
        if (index < 0)
        {
            throw new ZhilianFileReadFail();
        }
        return index;
    }
    public class ZhilianFileReadFail : NotSupportedException
    {
        public ZhilianFileReadFail():base("文件不是预期的格式")
        {
        }
    }
    private static readonly Regex MapTablesReg = new Regex(@"\<table class=MsoTableGrid.*?\<span style='font-size:.*?>(.*?)\</span\>.*?\<span style='font-size:.*?>(.*?)\</span\>.*?\<span style='font-size:.*?>(.*?)\</span\>.*?\<span style='font-size:.*?>.*?\</span\>.*?\<span style='font-size:.*?>(.*?)\</span\>.*?\</table\>", RegexOptions.Singleline|RegexOptions.Compiled);
    private static readonly Regex MapTablesSkillsReg = new Regex(@"\<table class=MsoTableGrid.*?\<span style='font-size:.*?>(.*?)\</span\>.*?\<span style='font-size:.*?>.*?\</span\>.*?\<span style='font-size:.*?>(.*?)\</span\>.*?\</table\>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex MapDateName = new Regex(@"^(\d{4}\.\d{2})\s*\-\s*(\d{4}\.\d{2}|至今)(.*?)$",RegexOptions.Compiled);
    private static readonly Regex MapTableOnlyReg = new Regex(@"\<table class=MsoTableGrid.*?\</table\>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex MapSpanReg = new Regex(@"\<span style='font-size:.*?>(.*?)\</span\>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex MapHtmlReg = new Regex(@"\<[^\<\>]+?\>", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly string[] checkAnchors = new string[] {
        "<body lang=",
        "<div class=Section1",
        "<table class=MsoTableGrid",
        "<table border=0",
        "<span style='font-size:",
        "</table>",
        "<tr style='mso-yfti-irow:1'>",
        "<p class=MsoNormal",
        "<span style='font-size:",
        "</p>",
        "<p class=MsoNormal",
        "<span style='font-size",
        "</p>",
        "<td rowspan=2",
        "<v:imagedata src=\"http://mypics.zhaopin.cn",
    };
    private bool findAllStrings(Stream stream,IEnumerable<string> strList)
    {
        foreach (var str in strList)
        {
            long index = readToString(stream,str);
            if (index < 0)
            {
                return false;
            }
        }
        return true;
    }
    private Memory<byte> memory;
    private string lastString => Encoding.UTF8.GetString(memory.Span);
    private long readToString(Stream stream,string str,bool posToSearchEnd =false) {
        Span<byte> strB = Encoding.UTF8.GetBytes(str);
        int bufferSize = Math.Max(strB.Length * 100, 1024);
        Span<byte> buffer = new Span<byte>(new byte[bufferSize]);
        var searchLen = strB.Length;
        while (true) {
            long thisLen = stream.Read(buffer);
            if (thisLen < searchLen)
            {
                return -1;
            }
            long index = buffer.Slice(0,(int)thisLen).IndexOf(strB);
            if (index>=0)
            {
                memory = buffer.Slice(0,(int)index).ToArray().AsMemory();
                stream.Position -= (thisLen - index - (posToSearchEnd?searchLen:0));
                return stream.Position;
            }

            stream.Position -= (searchLen - 1);
        }
    }
}

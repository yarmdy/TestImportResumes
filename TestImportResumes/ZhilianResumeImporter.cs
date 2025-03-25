using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public class ZhilianResumeImporter : ResumeImporter
{
    private IDicToObjConverter _myConverter;
    public ZhilianResumeImporter(IDicToObjConverter myConverter)
    {
        _myConverter = myConverter;
    }
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
        Dictionary<string,object?> resultDic = new Dictionary<string, object?>();
        try
        {
            long streamEnd = stream.Seek(0, SeekOrigin.End);
            stream.Position = 0;
            long start = readToStringRequire(stream, "<table class=MsoTableGrid");
            readToStringRequire(stream, "<tr style='mso-yfti-irow:2");
            long end = readToStringRequire(stream, "<span style='font-size:15.0pt;");
            stream.Position = start;
            baseInfo(stream,resultDic,end);
            start = stream.Position;
            while (end>=0) { 
                start = readToString(stream, "<span style='font-size:15.0pt;", posToSearchEnd:true);
                end = readToString(stream, "<span style='font-size:15.0pt;");
                stream.Position = start;

                readToStringRequire(stream, ">", end,true);
                readToStringRequire(stream, "<", end,false);
                long maxEnd = end < 0 ? streamEnd : end;
                switch (getLastString(stream)!)
                {
                    case "求职意向":
                        {
                            Yixiang(stream, resultDic, maxEnd);
                        }
                        break;
                    case "自我评价":
                        {
                            Pingjia(stream, resultDic, maxEnd);
                        }
                        break;
                    case "工作经历":
                        {
                            Gongzuo(stream, resultDic, maxEnd);
                        }
                        break;
                    case "项目经历":
                        {
                            Xiangmu(stream, resultDic, maxEnd);
                        }
                        break;
                    case "教育经历":
                        {
                            Jiaoyu(stream, resultDic, maxEnd);
                        }
                        break;
                    case "证书":
                        {
                            Zhengshu(stream, resultDic, maxEnd);
                        }
                        break;
                    case "专业技能":
                        {
                            Jineng(stream, resultDic, maxEnd);
                        }
                        break;
                    default:
                        {
                            break;
                        }

                }
                if (end >= 0)
                {
                    stream.Position = end;
                }
            }
            resultDic["ExpectedSalary"] = MapGZReg.Match(resultDic["ExpectedSalary"] + "")?.Groups[1].Value;
            resultDic["Exp"] = (resultDic["Exp"] + "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
            resultDic["Birth"] = MapBirthReg.Match(resultDic["Birth"] + "")?.Groups[1].Value;
            

            ZZ_XQ_Resumes_Entity obj = _myConverter.Convert<ZZ_XQ_Resumes_Entity>(resultDic);
            return Task.FromResult(ImportResult.Success(ResumeSource, obj));
        }catch(ZhilianFileReadFail zlex)
        {
            return Task.FromResult(ImportResult.Error(ResumeSource, zlex));
        }catch(Exception ex)
        {
            return Task.FromResult(ImportResult.Error(ResumeSource, ex));
        }
    }

    private void Yixiang(Stream stream, Dictionary<string,object?> dic, long end)
    {
        long start = readToStringRequire(stream, "<table class=MsoTableGrid");
        readToStringRequire(stream, "期望工作地区：");
        readToStringRequire(stream, "<span style='font-size:");
        readToStringRequire(stream, ">");
        readToStringRequire(stream, "<");
        string PreferredLocation = getLastString(stream)!.Trim();
        stream.Position = start;
        readToStringRequire(stream, "期望月薪：");
        readToStringRequire(stream, "<span lang=EN-US style='font-size:");
        readToStringRequire(stream, ">");
        readToStringRequire(stream, "<");
        string ExpectedSalary = getLastString(stream)!.Trim();
        stream.Position = start;
        readToStringRequire(stream, "期望工作性质：");
        readToStringRequire(stream, "<span style='font-size:");
        readToStringRequire(stream, ">");
        readToStringRequire(stream, "<");
        string JobDescription = getLastString(stream)!.Trim();
        stream.Position = start;
        readToStringRequire(stream, "期望从事职业：");
        readToStringRequire(stream, "<span style='font-size:");
        readToStringRequire(stream, ">");
        readToStringRequire(stream, "<");
        string Goal = getLastString(stream)!.Trim();
        stream.Position = start;
        readToStringRequire(stream, "期望从事行业：");
        readToStringRequire(stream, "<span style='font-size:");
        readToStringRequire(stream, ">");
        readToStringRequire(stream, "<");
        string Industry = getLastString(stream)!.Trim();
        dic["PreferredLocation"] = PreferredLocation;
        dic["ExpectedSalary"] = ExpectedSalary;
        dic["Goal"] = Goal;
        dic["Industry"] = Industry;
        dic["JobDescription"] = JobDescription;
    }
    private void Pingjia(Stream stream, Dictionary<string,object?> dic, long end)
    {
        readToStringRequire(stream, "<span style='font-size:");
        readToStringRequire(stream, ">");
        readToStringRequire(stream, "</span>");
        string pingjia = getLastString(stream)!.Trim().Replace("<br/>", "\r\n");
    }
    private void Gongzuo(Stream stream, Dictionary<string,object?> dic, long end)
    {
        long start = stream.Position;
        Memory<byte> tmpData = new Memory<byte>(new byte[end - start]);
        stream.Read(tmpData.Span);
        string gongzuoData = GetEncoding(stream).GetString(tmpData.Span);
        MatchCollection matches = MapTablesReg.Matches(gongzuoData);
        string WorkExperience = string.Join("\r\n", matches.Select(a => string.Join("\r\n", a.Groups.Values.Skip(1).Select((b, i) => {
            string val = b.Value.Trim().Replace("&nbsp;", " ").Replace("<br/>", "\r\n");
            return i switch
            {
                2 => val,
                3 => val,
                _ => string.Join("\r\n", val.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.TrimEntries))
            };
        }))));
        dic["WorkExperience"] = WorkExperience;
    }
    private void Xiangmu(Stream stream, Dictionary<string,object?> dic, long end)
    {
        long start = stream.Position;
        Memory<byte> tmpData = new Memory<byte>(new byte[end - start]);
        stream.Read(tmpData.Span);
        string xiangmuData = GetEncoding(stream).GetString(tmpData.Span);
        MatchCollection matches = MapTablesSkillsReg.Matches(xiangmuData);
        string Projects = JsonSerializer.Serialize(matches.Select(a => {
            string nameAndTime = a.Groups[1].Value.Trim().Replace("&nbsp;", " ").Replace("<br/>", "\r\n");
            Match match = MapDateName.Match(nameAndTime);
            string date_1 = match.Success ? match.Groups[2].Value : "";
            return new
            {
                ProjectName = match.Success ? match.Groups[3].Value : "",
                Date = match.Success ? match.Groups[1].Value : "",
                Date_1 = date_1 == "至今" ? "" : date_1,
                Role = "",
                ProjectDescription = a.Groups[2].Value.Trim().Replace("&nbsp;", " ").Replace("<br/>", "\r\n"),
                Technologies = "",
            };
        }).ToArray());
        dic["Projects"] = Projects;
    }
    private void Jiaoyu(Stream stream, Dictionary<string,object?> dic, long end)
    {
        long start = stream.Position;
        Memory<byte> tmpData = new Memory<byte>(new byte[end - start]);
        stream.Read(tmpData.Span);
        string jiaoyuData = GetEncoding(stream).GetString(tmpData.Span);

        string Education = string.Join("\r\n", MapTableOnlyReg.Matches(jiaoyuData).Select(a => string.Join("\r\n", MapSpanReg.Matches(a.Value).Select((b, i) => {
            string val = MapHtmlReg.Replace(b.Groups[1].Value, "").Trim().Replace("&nbsp;", " ").Replace("<br/>", "\r\n");
            return string.Join("\r\n", val.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.TrimEntries));
        }))));
        dic["Education"] = Education;
    }
    private void Zhengshu(Stream stream, Dictionary<string,object?> dic, long end)
    {
        long start = stream.Position;
        Memory<byte> tmpData = new Memory<byte>(new byte[end - start]);
        stream.Read(tmpData.Span);
        string zhengshuData = GetEncoding(stream).GetString(tmpData.Span);
        MatchCollection matches = MapSpanReg.Matches(zhengshuData);
        string Certificates = JsonSerializer.Serialize(matches.Select(a => a.Groups[1].Value.Replace("&nbsp;", " ").Replace("<br/>", "\r\n").Trim()).Select(a => {
            string[] cerInfo = a.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new
            {
                CertificateName = cerInfo.Length < 2 ? cerInfo[0] : cerInfo[1],
                IssuedBy = "",
                Description = "",
                Date = cerInfo.Length < 2 ? "" : cerInfo[0],
                Date_1 = ""
            };
        }).ToArray());
        dic["Certificates"] = Certificates;
    }
    private void Jineng(Stream stream, Dictionary<string,object?> dic, long end)
    {
        long start = stream.Position;
        Memory<byte> tmpData = new Memory<byte>(new byte[end - start]);
        stream.Read(tmpData.Span);
        string jinengData = GetEncoding(stream).GetString(tmpData.Span);
        MatchCollection matches = MapSpanReg.Matches(jinengData);
        string Skills = JsonSerializer.Serialize(matches.Select(a => MapHtmlReg.Replace(a.Groups[1].Value,"").Replace("&nbsp;", " ").Replace("<br/>", "\r\n").Trim()).Select(a => {
            string[] cerInfo = a.Split(new[] { '：'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new
            {
                Skill = cerInfo[0],
                Level = cerInfo[1],
                Description = cerInfo[2],
            };
        }).ToArray());
        dic["Skills"] = Skills;
    }

    private void baseInfo(Stream stream,Dictionary<string,object?> dic,long end)
    {
        readToStringRequire(stream, "<span style='font-size:");
        readToStringRequire(stream, ">");
        readToStringRequire(stream, "<");
        string FullName = getLastString(stream)!;
        readToStringRequire(stream, "<span style='font-size:");
        readToStringRequire(stream, ">");
        readToStringRequire(stream, "<");
        string[] shenfen = getLastString(stream)!.Split(new[] { "&nbsp;" }, StringSplitOptions.RemoveEmptyEntries);
        string Sex = shenfen[0];
        string Birth = shenfen[1];
        string Exp = shenfen[2];
        string Edu = shenfen[3];
        readToStringRequire(stream, "<span style='font-size:");
        readToStringRequire(stream, ">");
        readToStringRequire(stream, "<");
        string[] shenfen2 = getLastString(stream)!.Split('|', StringSplitOptions.TrimEntries);
        string NowCity = shenfen2[0];
        string Hukou = shenfen2[1];
        string WorkStatus = shenfen2[2];
        readToStringRequire(stream, "<v:imagedata src=\"");
        readToStringRequire(stream, "\"");
        string Photo = getLastString(stream)!;
        readToStringRequire(stream, "<tr style='mso-yfti-irow:2");
        readToStringRequire(stream, "<span style='font-size:");
        readToStringRequire(stream, ">");
        readToStringRequire(stream, "手机：");
        readToStringRequire(stream, "<br />");
        string Mobile = getLastString(stream)!;
        readToStringRequire(stream, "<a href=\"mailto:");
        readToStringRequire(stream, "\">");
        string EMail = getLastString(stream)!;
        readToStringRequire(stream, "</table>");
        dic["FullName"] = FullName;
        dic["Sex"] = Sex;
        dic["Birth"] = Birth;
        dic["Exp"] = Exp;
        dic["Edu"] = Edu;
        dic["Photo"] = Photo;
        dic["Mobile"] = Mobile;
        dic["EMail"] = EMail;

    }
    private long readToStringRequire(Stream stream, string str, long endPos = -1, bool posToSearchEnd = true)
    {
        long index = readToString(stream, str, endPos,posToSearchEnd);
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
    private static readonly Regex MapGZReg = new Regex(@"\d+?\s*-\s*(\d+).*$", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex MapBirthReg = new Regex(@"\((.+?)\)", RegexOptions.Singleline | RegexOptions.Compiled);

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
    Dictionary<Stream, string> cacheLastString = new Dictionary<Stream, string>();
    private void setLastString(Stream stream,Span<byte> span)
    {
        cacheLastString[stream] = GetEncoding(stream).GetString(span);
    }
    private string? getLastString(Stream stream)
    {
        return cacheLastString.GetValueOrDefault(stream);
    }
    private long readToString(Stream stream,string str,long endPos = -1,bool posToSearchEnd =false) {
        long start = stream.Position;
        long end = endPos;
        Span<byte> strB = GetEncoding(stream).GetBytes(str);
        int bufferSize = Math.Max(strB.Length * 100, 1024);
        byte[] bufferData = new byte[bufferSize];
        Span<byte> buffer = new Span<byte>(bufferData);
        var searchLen = strB.Length;
        while (true) {
            long maxLen = bufferSize;
            if (endPos >= 0)
            {
                maxLen = Math.Min(maxLen, endPos - stream.Position);
            }
            long thisLen = stream.Read(bufferData, 0,(int)maxLen);
            if (thisLen < searchLen)
            {
                return -1;
            }
            long index = buffer.Slice(0,(int)thisLen).IndexOf(strB);
            if (index>=0)
            {
                setLastString(stream, buffer.Slice(0, (int)index));
                stream.Position -= (thisLen - index - (posToSearchEnd?searchLen:0));
                return stream.Position;
            }

            stream.Position -= (searchLen - 1);
        }
    }
    private readonly ConcurrentDictionary<Stream,Encoding> cacheEncoding = new ConcurrentDictionary<Stream,Encoding>();
    private Encoding GetEncoding(Stream stream) {
        return cacheEncoding.GetOrAdd(stream, stream => {
            Encoding _encoding = Encoding.UTF8;
            long t = stream.Position;
            stream.Position = 0;
            Span<byte> boom = new byte[4];
            int _byteLen = stream.Read(boom);
            if (_byteLen < 2)
            {
                return _encoding;
            }
            if (boom[0] == 0xFE && boom[1] == 0xFF)
            {
                // Big Endian Unicode
                _encoding = Encoding.BigEndianUnicode;
            }
            else if (boom[0] == 0xFF && boom[1] == 0xFE)
            {
                // Little Endian Unicode, or possibly little endian UTF32
                if (_byteLen < 4 || boom[2] != 0 || boom[3] != 0)
                {
                    _encoding = Encoding.Unicode;
                }
                else
                {
                    _encoding = Encoding.UTF32;
                }
            }
            else if (_byteLen >= 4 && boom[0] == 0 && boom[1] == 0 &&
                boom[2] == 0xFE && boom[3] == 0xFF)
            {
                // Big Endian UTF32
                _encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
            }
            
            stream.Position = t;
            return _encoding;
        });
    }
}

public interface IDicToObjConverter
{
    T Convert<T>(IDictionary<string, object?> dic) where T : class, new();
}

public class DynamicDicToObjConverter : IDicToObjConverter
{
    private ConcurrentDictionary<Type, Func<IDictionary<string, object?>, object>> _cacheFunc = new ConcurrentDictionary<Type, Func<IDictionary<string, object?>, object>>();

    public T Convert<T>(IDictionary<string, object?> dic) where T : class, new()
    {
        return (T)Convert(typeof(T), dic);
    }

    private static readonly MethodInfo ContainsKey = typeof(IDictionary<string, object?>).GetMethod("ContainsKey", BindingFlags.Public | BindingFlags.Instance)!;
    private static readonly PropertyInfo Item = typeof(IDictionary<string, object?>).GetProperty("Item", BindingFlags.Public | BindingFlags.Instance)!;
    private static bool isSameType(Type type1, object obj2)
    {
        Type type2 = obj2.GetType();
        if (type1.IsAssignableFrom(type2))
        {
            return true;
        }
        type1 = Nullable.GetUnderlyingType(type1) ?? type1;
        type2 = Nullable.GetUnderlyingType(type2) ?? type2;
        if (type1.IsAssignableFrom(type2))
        {
            return true;
        }
        return false;
    }
    private static bool canConvert(Type type1, object obj2)
    {
        Type type2 = obj2.GetType();
        type1 = Nullable.GetUnderlyingType(type1) ?? type1;
        type2 = Nullable.GetUnderlyingType(type2) ?? type2;
        
        return true;
    }
    private static object? convertTo(object obj2, Type type)
    {
        try
        {
            return System.Convert.ChangeType(obj2, type);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        return null;
    }
    public object Convert(Type type, IDictionary<string, object?> dic)
    {
        return _cacheFunc.GetOrAdd(type, type =>
        {
            ParameterExpression paramDic = Expression.Parameter(typeof(IDictionary<string, object?>), nameof(dic));
            LabelTarget returnLabel = Expression.Label(type, "return");
            ParameterExpression localResult = Expression.Parameter(type, "result");

            ConstructorInfo? constructor = type.GetConstructor(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, Array.Empty<Type>());
            if (constructor == null)
            {
                throw new ArgumentException("没有公共无参的构造");
            }
            List<Expression> expressions = new List<Expression>()
            {
                Expression.Assign(localResult, Expression.New(constructor)),
            };

            //System.Convert.GetTypeCode()
            foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(a => a.GetMethod != null & a.GetCustomAttribute<IgnoreConvertAttribute>(true) == null))
            {
                ParameterExpression obj = Expression.Parameter(typeof(object), "obj");
                Expression propName = Expression.Constant(prop.Name);
                Expression propType = Expression.Constant(prop.PropertyType);
                Type real = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                Expression realType = Expression.Constant(real);
                Expression dicIndex = Expression.MakeIndex(paramDic, Item, new Expression[] { propName });

                Expression ifthen = Expression.IfThen(
                        Expression.AndAlso(
                            Expression.Call(paramDic, ContainsKey, propName),
                            Expression.NotEqual(Expression.Constant(null), dicIndex)
                            ),
                        Expression.IfThenElse(
                            Expression.Call(null, ((Delegate)isSameType).Method, new Expression[] { propType, dicIndex }),
                            Expression.Assign(
                                Expression.Property(localResult, prop),
                                Expression.Convert(dicIndex, prop.PropertyType)),
                            Expression.IfThen(
                                Expression.Call(null, ((Delegate)canConvert).Method, propType, dicIndex),
                                Expression.Block(
                                    new ParameterExpression[] { obj },
                                    Expression.Assign(obj, Expression.Call(null, ((Delegate)convertTo).Method, dicIndex, realType)),
                                    Expression.IfThen(
                                        Expression.NotEqual(obj, Expression.Constant(null)),
                                        Expression.Assign(
                                            Expression.Property(localResult, prop),
                                            Expression.Convert(obj, prop.PropertyType)
                                            )
                                        )
                                    )
                            )
                        )
                    );
                expressions.Add(ifthen);
            }



            expressions.Add(Expression.Return(returnLabel, localResult));
            expressions.Add(Expression.Label(returnLabel, localResult));

            Expression block = Expression.Block(
                new ParameterExpression[] {
                    localResult
                },
                expressions
                );
            return Expression.Lambda<Func<IDictionary<string, object?>, object>>(block, paramDic).Compile();
        }).Invoke(dic);
    }
}
public class IgnoreConvertAttribute : Attribute{ }
public class ZZ_XQ_Resumes_Entity
{
    public string? Photo { get; set; }
    public int ZZ_XQ_ResumesID { get; set; }
    public string? ResumeName { get; set; }
    public int ZZ_XQ_StudentsID { get; set; }
    public string? FullName { get; set; }
    public string? Sex { get; set; }
    public string? EMail { get; set; }
    public string? Mobile { get; set; }
    public string? Address { get; set; }
    public int? ZZ_XQ_ClassesID { get; set; }
    public int? ZZ_XQ_OrganizationsID { get; set; }
    public int? ZZ_XQ_TeachersID { get; set; }
    public DateTime? Date { get; set; }
    public string? TalentSkills { get; set; }
    public string? Achievements { get; set; }
    public string? RecommendedPosition { get; set; }
    public string? RecommendedRemarks { get; set; }
    public int? RecommendedZZ_XQ_OrganizationsID { get; set; }
    public string? Skills { get; set; }
    public string? Certificates { get; set; }
    public string? Projects { get; set; }
    public string? WorkExperience { get; set; }
    public string? Education { get; set; }
    public string? Industry { get; set; }
    public string? PreferredLocation { get; set; }
    public int? ExpectedSalary { get; set; }
    public string? JobDescription { get; set; }
    public string? Goal { get; set; }
    public string? Plan { get; set; }
    public DateTime? Birth { get; set; }
    public int? Exp { get; set; }
    public string? Edu { get; set; }
    public DateTime TimeUpdated { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime TimeCreated { get; set; }
    public string? CreatedBy { get; set; }
}
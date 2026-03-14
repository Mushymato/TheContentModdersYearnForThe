using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Sickhead.Engine.Util;
using TheContentModdersYearnForThe.Foreach;

namespace TheContentModdersYearnForThe.Majigs;

public interface IDrill
{
    public Type FldType { get; }
    public bool TryGet(object? subject, [NotNullWhen(true)] out object? following);
    public void Set(object? subject, Func<JToken?> valueGet, BoundDrill? prior = null);
}

public record TraverseGetTriple(string Key, object Subject, BoundDrill Drill);

public interface ITraversableDrill : IDrill
{
    public bool IsWild { get; }
    public List<TraverseGetTriple>? TraverseGet(object? subject);
}

public record BoundDrill(object Subject, IDrill Drill)
{
    public bool TryGet(out object? following) => Drill.TryGet(Subject, out following);

    public void Set(Func<JToken?> valueGet) => Drill.Set(Subject, valueGet);
}

public sealed record ModelFieldDrill(Type FldType, FieldInfo FldInfo) : IDrill
{
    public bool TryGet(object? subject, [NotNullWhen(true)] out object? following)
    {
        following = FldInfo.GetValue(subject);
        return following != null;
    }

    public void Set(object? subject, Func<JToken?> valueGet, BoundDrill? prior = null)
    {
        FldInfo!.SetValue(subject, valueGet()?.ToObject(FldType));
    }
}

public sealed record ModelPropertyDrill(Type FldType, PropertyInfo PropInfo) : IDrill
{
    public bool TryGet(object? subject, [NotNullWhen(true)] out object? following)
    {
        following = PropInfo.GetValue(subject);
        return following != null;
    }

    public void Set(object? subject, Func<JToken?> valueGet, BoundDrill? prior = null)
    {
        PropInfo!.SetValue(subject, valueGet()?.ToObject(FldType));
    }
}

public sealed record StringDictDrill<TFldType>(Type FldType, string Key) : ITraversableDrill
{
    public bool IsWild => Key == DrillFactory.WILDCARD;

    public bool TryGet(object? subject, [NotNullWhen(true)] out object? following)
    {
        if ((subject as IDictionary<string, TFldType?>)?.TryGetValue(Key, out TFldType? typedValue) ?? false)
        {
            following = typedValue;
            return following != null;
        }
        following = null;
        return false;
    }

    public void Set(object? subject, Func<JToken?> valueGet, BoundDrill? prior = null)
    {
        (subject as IDictionary<string, TFldType?>)?[Key] = (TFldType?)(valueGet()?.ToObject(FldType));
    }

    public List<TraverseGetTriple>? TraverseGet(object? subject)
    {
        if (!IsWild)
            return null;
        if (subject is not IDictionary<string, TFldType?> subjectDict)
            return null;
        List<TraverseGetTriple> boundDrills = [];
        foreach ((string key, TFldType? mem) in subjectDict)
        {
            if (mem == null)
                continue;
            boundDrills.Add(new(key, mem, new BoundDrill(subject, new StringDictDrill<TFldType>(FldType, key))));
        }
        return boundDrills;
    }
}

public sealed record IdListDrill<TFldType>(Type FldType, string Id, IDrill IdDrill) : ITraversableDrill
{
    public bool IsWild => Id == DrillFactory.WILDCARD;
    public string? WildKey { get; set; } = null;

    public bool TryGet(object? subject, [NotNullWhen(true)] out object? following)
    {
        following = null;
        if (subject is not IList<object?> priorList)
            return false;
        foreach (object? obj in priorList)
        {
            if (TryGetId(obj, out string? id) && id == Id)
            {
                following = obj;
                return following != null;
            }
        }
        return false;
    }

    public void Set(object? subject, Func<JToken?> valueGet, BoundDrill? prior = null)
    {
        if (subject is not IList<object?> priorList)
            return;
        for (int i = 0; i < priorList.Count; i++)
        {
            object? obj = priorList[i];
            if (TryGetId(obj, out string? id) && id == Id)
            {
                priorList[i] = valueGet()?.ToObject(FldType);
            }
        }
    }

    private bool TryGetId(object? obj, [NotNullWhen(true)] out string? idStr)
    {
        if (IdDrill.TryGet(obj, out object? idObj))
        {
            idStr = (string?)idObj;
            return idStr != null;
        }
        idStr = null;
        return false;
    }

    public List<TraverseGetTriple>? TraverseGet(object? subject)
    {
        if (!IsWild)
            return null;
        if (subject is not IList<TFldType?> subjectList)
            return null;
        List<TraverseGetTriple> boundDrills = [];
        int idx = 0;
        foreach (TFldType? mem in subjectList)
        {
            if (mem == null || !TryGetId(mem, out string? idStr))
                continue;
            idx++;
            boundDrills.Add(
                new(idx.ToString(), mem, new BoundDrill(subject, new IdListDrill<TFldType>(FldType, idStr, IdDrill)))
            );
        }
        return boundDrills;
    }
}

public sealed record RegexStringDrill(Regex RegExp, Regex? SplitOn) : IDrill
{
    public Type FldType => typeof(string);
    private static readonly StringBuilder sb = new();

    public bool TryGet(object? subject, [NotNullWhen(true)] out object? following)
    {
        following = null;
        return false;
    }

    public void Set(object? subject, Func<JToken?> valueGet, BoundDrill? prior = null)
    {
        if (subject is not string strSubj)
            return;
        JToken? value = valueGet();
        if (value == null)
        {
            prior?.Set(() => value);
            return;
        }
        string replSubj;
        string replStr = value.ToString();
        if (SplitOn == null)
        {
            replSubj = RegExp.Replace(strSubj, replStr);
        }
        else
        {
            int prevIdx = 0;
            foreach (Match match in SplitOn.Matches(strSubj))
            {
                sb.Append(RegExp.Replace(strSubj[prevIdx..match.Index], replStr));
                prevIdx = match.Index + match.Length;
                sb.Append(strSubj[match.Index..prevIdx]);
                replStr = valueGet()?.ToString() ?? replStr;
            }
            if (prevIdx < strSubj.Length)
            {
                sb.Append(RegExp.Replace(strSubj[prevIdx..], replStr));
            }
            replSubj = sb.ToString();
            sb.Clear();
        }
        prior?.Set(() => replSubj);
    }
}

public static class DrillFactory
{
    public const string WILDCARD = "*";

    public static bool CheckEq(this IDrill drill, object? subject, JToken? value)
    {
        if (drill.TryGet(subject, out object? following))
            return following.Equals(value?.ToObject(drill.FldType));
        return false;
    }

    public static bool TryMake(Type typ, FieldData fieldName, [NotNullWhen(true)] out IDrill? drill)
    {
        if (typ.IsGenericType && TryMakeTraversable(typ, fieldName, out ITraversableDrill? tDrill))
        {
            drill = tDrill;
            return drill != null;
        }
        if (fieldName == WILDCARD)
        {
            drill = null;
            return false;
        }

        drill = null;
        if (fieldName.Split != null)
        {
            Regex regExp = new(fieldName.Match);
            Regex? splitOn = fieldName.Split.Length > 0 ? new(fieldName.Split) : null;
            drill = new RegexStringDrill(regExp, splitOn);
        }
        else if (typ.GetProperty(fieldName.Match, BindingFlags.Instance | BindingFlags.Public) is PropertyInfo property)
        {
            drill = new ModelPropertyDrill(property.GetDataType(), property);
        }
        if (typ.GetField(fieldName.Match, BindingFlags.Instance | BindingFlags.Public) is FieldInfo field)
        {
            drill = new ModelFieldDrill(field.FieldType, field);
        }
        return drill != null;
    }

    public static bool TryMakeTraversable(
        Type typ,
        FieldData fieldName,
        [NotNullWhen(true)] out ITraversableDrill? drill
    )
    {
        Type genericDef = typ.GetGenericTypeDefinition();
        Type[] genericArgs = typ.GetGenericArguments();
        if (
            (genericDef == typeof(Dictionary<,>) || genericDef == typeof(IDictionary<,>))
            && genericArgs[0] == typeof(string)
        )
        {
            Type genericDrillType = typeof(StringDictDrill<>).MakeGenericType([genericArgs[1]]);
            drill = (ITraversableDrill?)Activator.CreateInstance(genericDrillType, genericArgs[1], fieldName.Match);
            return drill != null;
        }
        else if (
            (genericDef == typeof(List<>) || genericDef == typeof(IList<>))
            && (TryMake(genericArgs[0], "Id", out IDrill? idDrill) || TryMake(genericArgs[0], "ID", out idDrill))
        )
        {
            Type genericDrillType = typeof(IdListDrill<>).MakeGenericType([genericArgs[0]]);
            drill = (ITraversableDrill?)
                Activator.CreateInstance(genericDrillType, genericArgs[0], fieldName.Match, idDrill);
            return drill != null;
        }
        drill = null;
        return false;
    }
}

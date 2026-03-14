using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using TheContentModdersYearnForThe.Majigs;

namespace TheContentModdersYearnForThe.Foreach;

public record FieldData(string Match, string? Split)
{
    public static implicit operator FieldData(string value) => new(value, null);
}

public sealed class ForeachFieldPair
{
    public List<FieldData>? Fields { get; set; } = null;
    public JToken? Value { get; set; } = null!;
    public List<JToken?>? RandValues { get; set; } = null;
    public string? ValueFromKey { get; set; } = null;
    public bool Truthy { get; set; } = true;

    private bool isValid = true;
    private List<IDrill>? drills = null;
    private bool drillChecksKey = false;

    private JToken? GetPickedValue(object? sourceObj = null)
    {
        if (sourceObj == null)
            return RandValues != null ? Random.Shared.ChooseFrom(RandValues) : Value;
        if (drills == null || drills.Count < 0)
            return null;
        object? targetObj = sourceObj;
        for (int i = 0; i < drills.Count - 1; i++)
        {
            if (!drills[i].TryGet(targetObj, out targetObj))
                return null;
        }
        if (!drills[^1].TryGet(targetObj, out object? following))
        {
            return null;
        }
        return JToken.FromObject(following);
    }

    public bool Validate(Type typ)
    {
        if (!isValid)
            return false;
        if (Fields == null || Fields.Count == 0)
        {
            isValid = false;
            return false;
        }
        if (drills != null)
            return true;

        if (Fields.Count == 1)
        {
            if (Fields[0] == ".")
            {
                drills = [];
                isValid = true;
                return true;
            }
            else if (Fields[0] == "..")
            {
                drills = [];
                isValid = true;
                drillChecksKey = true;
                return true;
            }
        }

        drills = [];
        Type prevTyp = typ;
        for (int i = 0; i < Fields.Count; i++)
        {
            if (DrillFactory.TryMake(prevTyp, Fields[i], out IDrill? drill))
            {
                drills.Add(drill);
                prevTyp = drill.FldType;
            }
            else
            {
                drills = null;
                isValid = false;
                return false;
            }
        }
        if (drills.Count == 0)
        {
            drills = null;
            isValid = false;
            return false;
        }

        return true;
    }

    public void RecursiveSet(IDrill drill, object? subject, int drillsIdx, object? sourceObj, BoundDrill? prior)
    {
        if (drills == null || drillsIdx > drills.Count)
            return;

        if (drill is ITraversableDrill travDrill && travDrill.IsWild)
        {
            if (drillsIdx < drills.Count)
            {
                RecursiveSet(travDrill, subject, drillsIdx, sourceObj);
            }
            return;
        }

        if (drillsIdx >= drills.Count)
        {
            drill.Set(subject, () => GetPickedValue(sourceObj), prior);
        }
        else if (drill.TryGet(subject, out object? following))
        {
            IDrill nextDrill = drills[drillsIdx];
            RecursiveSet(nextDrill, following, drillsIdx + 1, sourceObj, new(following, nextDrill));
        }
    }

    public void RecursiveSet(
        ITraversableDrill drill,
        object? subject,
        int drillsIdx,
        object? sourceObj,
        List<ForeachFieldPair>? filters = null
    )
    {
        if (drills == null || drillsIdx > drills.Count)
            return;

        IDrill? nextDrill = drillsIdx >= drills.Count ? null : drills[drillsIdx];
        if (drill.TraverseGet(subject) is List<TraverseGetTriple> boundDrills)
        {
            foreach ((string key, object following, BoundDrill bound) in boundDrills)
            {
                if (filters != null && filters.Any(flt => !flt.FilterMatches(key, following)))
                    continue;

                if (nextDrill == null)
                    bound.Set(() => GetPickedValue(sourceObj));
                else
                    RecursiveSet(nextDrill, following, drillsIdx + 1, sourceObj, bound);
            }
        }
    }

    public bool FilterMatches(string key, object? targetObj)
    {
        if (drills == null || drills.Count < 0)
            return false;
        if (drillChecksKey)
            return Truthy == (key == GetPickedValue()?.ToString());
        for (int i = 0; i < drills.Count - 1; i++)
        {
            if (!drills[i].TryGet(targetObj, out targetObj))
                return false;
        }
        return Truthy == drills[^1].CheckEq(targetObj, GetPickedValue());
    }
}

public sealed class ForeachData
{
    public string Target { get; set; } = null!;
    public bool TargetIsPrefix { get; set; } = false;
    public string? CopyFromAsset { get; set; } = null;
    public List<ForeachFieldPair>? Filter { get; set; } = null!;
    public List<ForeachFieldPair> Modify { get; set; } = null!;
    public int Precedence { get; set; } = 0;

    internal IAssetName TargetAssetName
    {
        get => field ??= ModEntry.content.ParseAssetName(Target);
        private set => field = value;
    } = null;

    internal IAssetName? CopyFromAssetName
    {
        get => string.IsNullOrEmpty(CopyFromAsset) ? null : field ??= ModEntry.content.ParseAssetName(CopyFromAsset);
        private set => field = value;
    } = null;

    internal bool MatchesTargetAssetName(IAssetName assetName)
    {
        if (TargetIsPrefix)
            return assetName.IsDirectlyUnderPath(TargetAssetName?.BaseName);
        else
            return assetName.IsEquivalentTo(TargetAssetName);
    }

    private bool isValid = true;
    private Action<IAssetData>? cachedEdit = null;

    private void EditStringModelDictionary<TValue>(IAssetData asset)
    {
        if (Filter != null && Filter.Any(flt => !flt.Validate(typeof(TValue))))
            return;
        if (
            !DrillFactory.TryMakeTraversable(
                typeof(IDictionary<string, TValue>),
                DrillFactory.WILDCARD,
                out ITraversableDrill? drill
            )
        )
        {
            return;
        }
        IDictionary<string, TValue> data = asset.AsDictionary<string, TValue>().Data;
        foreach (ForeachFieldPair modify in Modify)
        {
            TValue? sourceObj = default;
            if (modify.ValueFromKey != null)
            {
                if (!data.TryGetValue(modify.ValueFromKey, out sourceObj))
                {
                    sourceObj = default;
                }
            }
            if (!modify.Validate(typeof(TValue)))
                continue;
            modify.RecursiveSet(drill, data, 0, sourceObj, Filter);
        }
    }

    private void EditModelList<TValue>(IAssetData asset)
    {
        if (Filter != null && Filter.Any(flt => !flt.Validate(typeof(TValue))))
            return;
        if (
            !DrillFactory.TryMakeTraversable(typeof(IList<TValue>), DrillFactory.WILDCARD, out ITraversableDrill? drill)
        )
            return;
        IList<TValue> data = asset.GetData<IList<TValue>>();
        foreach (ForeachFieldPair modify in Modify)
        {
            if (!modify.Validate(typeof(TValue)))
                continue;

            modify.RecursiveSet(drill, data, 0, Filter);
        }
    }

    private void CopyFromTexture2D(IAssetData asset)
    {
        if (
            string.IsNullOrEmpty(CopyFromAsset)
            || asset.NameWithoutLocale.IsEquivalentTo(CopyFromAsset)
            || !Game1.content.DoesAssetExist<Texture2D>(CopyFromAsset)
        )
        {
            return;
        }
        ModEntry.LogOnce($"Copy '{CopyFromAsset}' to '{asset.NameWithoutLocale}'");
        IAssetDataForImage editor = asset.AsImage();
        Texture2D sourceImage = Game1.content.Load<Texture2D>(CopyFromAsset);
        editor.ExtendImage(sourceImage.Width, sourceImage.Height);
        editor.PatchImage(sourceImage, sourceImage.Bounds, sourceImage.Bounds, patchMode: PatchMode.Replace);
    }

    internal bool ValidateCachedEdit(AssetRequestedEventArgs e)
    {
        if (!isValid || cachedEdit != null)
        {
            return isValid;
        }
        Type typ = e.DataType;
        if (typ.IsGenericType)
        {
            Type genericDef = typ.GetGenericTypeDefinition();
            Type[] genericArgs = typ.GetGenericArguments();
            MethodInfo? editMethod = null;
            if (genericDef == typeof(Dictionary<,>) && genericArgs[0] == typeof(string))
            {
                editMethod = GetType()
                    .GetMethod(nameof(this.EditStringModelDictionary), BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.MakeGenericMethod(genericArgs[1]);
            }
            if (genericDef == typeof(List<>))
            {
                editMethod = GetType()
                    .GetMethod(nameof(this.EditModelList), BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.MakeGenericMethod(genericArgs[0]);
            }
            if (editMethod != null)
            {
                cachedEdit = (asset) => editMethod.Invoke(this, [asset]);
                return true;
            }
        }
        else if (typ == typeof(Texture2D) && CopyFromAssetName != null)
        {
            cachedEdit = CopyFromTexture2D;
            return true;
        }
        ModEntry.LogOnce($"Foreach not supported for {typ}");
        isValid = false;
        return false;
    }

    internal void ApplyEdit(AssetRequestedEventArgs e)
    {
        if (ValidateCachedEdit(e))
        {
            e.Edit(cachedEdit!, ModEntry.ReallyLateEdit);
        }
    }
}

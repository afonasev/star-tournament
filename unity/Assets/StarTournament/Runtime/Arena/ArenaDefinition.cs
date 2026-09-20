using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class ArenaGenerationRequest { public uint Seed; public string GeneratorVersion; public ProvingProfile Profile; }
    [Serializable] public sealed class ArenaSolid { public string Id, Support, Material; public Vector3 Position, Size; public Quaternion Rotation; public int Layer; }
    [Serializable] public sealed class ArenaSpawnRegion { public string Id, Support; public Vector3 Center, Size; }
    [Serializable] public sealed class ArenaDefinition
    {
        public uint Seed; public string GeneratorVersion, FamilyId, ProfileFingerprint, Identity;
        public ArenaSolid[] Solids; public Vector3[] Spawns, RouteAnchors;
        public ArenaSpawnRegion[] SpawnRegions; public NativeNavigationTransition[] Transitions;
        public ArenaDefinition Copy() => JsonUtility.FromJson<ArenaDefinition>(JsonUtility.ToJson(this));
    }

    /// <summary>Pure ARENA-2 generator; it deliberately consumes no match/session randomness.</summary>
    public static class ArenaGenerator
    {
        public const string FamiliesVersion = "unity-arena-families-v1";
        public const string FoundationVersion = FamiliesVersion; // Source compatibility for ARENA-1 callers.
        public static ArenaDefinition Generate(ArenaGenerationRequest request)
        {
            if (request == null || request.Profile == null || request.Profile.Validate().Count != 0) throw new ArgumentException("Invalid arena families profile");
            if (request.GeneratorVersion != FamiliesVersion) throw new ArgumentException("Unsupported arena generator version");
            var family=ArenaFamilyCatalog.Resolve(request.Profile.Get("arena.family"));
            var definition=ArenaFamilyCatalog.Build(family,request.Seed,request.Profile);
            definition.Seed=request.Seed;definition.GeneratorVersion=request.GeneratorVersion;definition.FamilyId=family;
            definition.ProfileFingerprint=Fingerprint(request.Profile);
            definition.Identity=$"{FamiliesVersion}|family:{family}|seed:{request.Seed}|profile:{definition.ProfileFingerprint}";
            definition.Solids=definition.Solids.OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray();Validate(definition);return definition;
        }
        public static void Validate(ArenaDefinition d)
        {
            if(d==null||string.IsNullOrWhiteSpace(d.Identity)||!ArenaFamilyCatalog.Contains(d.FamilyId)||d.Solids==null||d.Spawns==null||d.RouteAnchors==null||d.Transitions==null||d.SpawnRegions==null)throw new ArgumentException("Incomplete arena definition");
            var ids=new HashSet<string>();foreach(var s in d.Solids)if(s==null||string.IsNullOrWhiteSpace(s.Id)||!ids.Add(s.Id)||s.Size.x<=0||s.Size.y<=0||s.Size.z<=0||!Finite(s.Position)||!Finite(s.Size))throw new ArgumentException("Invalid arena solid");
            if(d.Transitions.Length<2)throw new ArgumentException("Arena family requires two transitions");
            ids.Clear();foreach(var t in d.Transitions)if(t==null||string.IsNullOrWhiteSpace(t.Id)||!ids.Add(t.Id)||string.IsNullOrWhiteSpace(t.LowerSupport)||string.IsNullOrWhiteSpace(t.UpperSupport)||t.OrderedFeet==null||t.OrderedFeet.Length<2)throw new ArgumentException("Invalid arena transition");
        }
        public static string Fingerprint(ProvingProfile p) => string.Join(";",p.Descriptors.OrderBy(d=>d.Path,StringComparer.Ordinal).Select(d=>d.Path+"="+p.Get(d.Path).ToString("R",CultureInfo.InvariantCulture)));
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
    }
}

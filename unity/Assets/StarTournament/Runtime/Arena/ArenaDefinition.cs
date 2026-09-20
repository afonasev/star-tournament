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
            definition.Solids=definition.Solids.OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray();Validate(definition,request.Profile);return definition;
        }
        public static void Validate(ArenaDefinition d, ProvingProfile profile=null)
        {
            var result=ArenaDefinitionValidator.Validate(d,profile??ProvingProfile.CreateArenaFamiliesDefault());
            if(!result.IsValid) throw new ArgumentException(result.ToString());
        }
        public static string Fingerprint(ProvingProfile p) => string.Join(";",p.Descriptors.OrderBy(d=>d.Path,StringComparer.Ordinal).Select(d=>d.Path+"="+p.Get(d.Path).ToString("R",CultureInfo.InvariantCulture)));
    }
}

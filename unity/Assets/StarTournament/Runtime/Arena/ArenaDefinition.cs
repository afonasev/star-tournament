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
        public ArenaSolid[] Solids; public Vector3[] Spawns, RouteAnchors; public Vector3 ArmorPickupAnchor, SpeedPickupAnchor, DamagePickupAnchor; public bool HasDamagePickup, DisableSpeedPickup;
        public ArenaSpawnRegion[] SpawnRegions; public NativeNavigationTransition[] Transitions;
        public ArenaDefinition Copy() => JsonUtility.FromJson<ArenaDefinition>(JsonUtility.ToJson(this));
    }

    /// <summary>ARENA-5 immutable pre-session hand-off; it is not match or replay state.</summary>
    public sealed class ArenaFreezeSnapshot
    {
        readonly ArenaDefinition definition;
        readonly ProvingProfile profile;
        ArenaFreezeSnapshot(ArenaDefinition definition, ProvingProfile profile)
        { this.definition=definition.Copy(); this.profile=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(profile)); }
        public string Identity => definition.Identity;
        public string FamilyId => definition.FamilyId;
        public string ProfileFingerprint => definition.ProfileFingerprint;
        public ArenaDefinition Definition => definition.Copy();
        public static ArenaFreezeSnapshot Create(ArenaDefinition definition, ProvingProfile profile)
        {
            if(definition==null || profile==null) throw new ArgumentException("ARENA_FREEZE_MISSING_INPUT|family:unknown|element:snapshot");
            var result=ArenaDefinitionValidator.Validate(definition,profile);
            if(!result.IsValid) throw new ArgumentException(result.ToString());
            if(!definition.ProfileFingerprint.StartsWith("authored:",StringComparison.Ordinal) && definition.ProfileFingerprint!=ArenaGenerator.Fingerprint(profile))
                throw new ArgumentException($"ARENA_FREEZE_PROFILE_MISMATCH|family:{definition.FamilyId}|element:profile");
            return new ArenaFreezeSnapshot(definition,profile);
        }
        public void RequireDefinition(ArenaDefinition candidate)
        {
            if(candidate==null || candidate.Identity!=definition.Identity || candidate.FamilyId!=definition.FamilyId ||
                candidate.ProfileFingerprint!=definition.ProfileFingerprint || JsonUtility.ToJson(candidate)!=JsonUtility.ToJson(definition))
                throw new ArgumentException($"ARENA_FREEZE_DEFINITION_MISMATCH|family:{FamilyId}|element:definition");
            if(!ProfileFingerprint.StartsWith("authored:",StringComparison.Ordinal) && ArenaGenerator.Fingerprint(profile)!=ProfileFingerprint)
                throw new ArgumentException($"ARENA_FREEZE_PROFILE_MISMATCH|family:{FamilyId}|element:profile");
        }
    }

    /// <summary>Pure ARENA-2 generator; it deliberately consumes no match/session randomness.</summary>
    public static class ArenaGenerator
    {
        public const string FamiliesVersion = "unity-arena-families-v1";
        public const string FoundationVersion = FamiliesVersion; // Source compatibility for ARENA-1 callers.
        public static ArenaDefinition Generate(ArenaGenerationRequest request) => Freeze(request).Definition;
        public static ArenaFreezeSnapshot Freeze(ArenaGenerationRequest request)
        {
            if (request == null || request.Profile == null || request.Profile.Validate().Count != 0) throw new ArgumentException("Invalid arena families profile");
            if (request.GeneratorVersion != FamiliesVersion) throw new ArgumentException("Unsupported arena generator version");
            var family=ArenaFamilyCatalog.Resolve(request.Profile.Get("arena.family"));
            var definition=ArenaFamilyCatalog.Build(family,request.Seed,request.Profile);
            definition.Seed=request.Seed;definition.GeneratorVersion=request.GeneratorVersion;definition.FamilyId=family;
            definition.ProfileFingerprint=Fingerprint(request.Profile);
            definition.Identity=$"{FamiliesVersion}|family:{family}|seed:{request.Seed}|profile:{definition.ProfileFingerprint}";
            definition.Solids=definition.Solids.OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray();
            return ArenaFreezeSnapshot.Create(definition,request.Profile);
        }
        public static void Validate(ArenaDefinition d, ProvingProfile profile=null)
        {
            var result=ArenaDefinitionValidator.Validate(d,profile??ProvingProfile.CreateArenaFamiliesDefault());
            if(!result.IsValid) throw new ArgumentException(result.ToString());
        }
        public static string Fingerprint(ProvingProfile p) => string.Join(";",p.Descriptors.OrderBy(d=>d.Path,StringComparer.Ordinal).Select(d=>d.Path+"="+p.Get(d.Path).ToString("R",CultureInfo.InvariantCulture)));
    }
}

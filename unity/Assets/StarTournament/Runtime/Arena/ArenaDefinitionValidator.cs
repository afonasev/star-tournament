using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Pure ARENA-4 gate. It validates canonical data before any Unity projection exists.</summary>
    public sealed class ArenaValidationResult
    {
        public readonly string Code, FamilyId, ElementId, Message;
        public bool IsValid => Code == null;
        ArenaValidationResult(string code, string familyId, string elementId, string message)
        { Code=code; FamilyId=familyId; ElementId=elementId; Message=message; }
        public static ArenaValidationResult Accept() => new ArenaValidationResult(null,null,null,null);
        public static ArenaValidationResult Reject(string code, string familyId, string elementId, string message) => new ArenaValidationResult(code,familyId,elementId,message);
        public override string ToString() => IsValid ? "Valid arena definition" : $"{Code}|family:{FamilyId ?? "unknown"}|element:{ElementId ?? "unknown"}|{Message}";
    }

    public static class ArenaDefinitionValidator
    {
        const float SurfaceTolerance = .05f;
        public static ArenaValidationResult Validate(ArenaDefinition definition, ProvingProfile profile)
        {
            string family=definition?.FamilyId;
            if(definition==null) return Reject("ARENA_NULL_DEFINITION",family,null,"Definition is required.");
            if(!ArenaFamilyCatalog.Contains(family)) return Reject("ARENA_UNKNOWN_FAMILY",family,null,"Family is not registered.");
            if(profile==null || profile.Validate().Count!=0) return Reject("ARENA_INVALID_PROFILE",family,null,"Arena profile is invalid.");
            if(string.IsNullOrWhiteSpace(definition.Identity)) return Reject("ARENA_MISSING_IDENTITY",family,null,"Definition identity is required.");
            if(definition.Solids==null || definition.Spawns==null || definition.RouteAnchors==null || definition.SpawnRegions==null || definition.Transitions==null)
                return Reject("ARENA_INCOMPLETE_DEFINITION",family,null,"Canonical arena collections are required.");

            var ids=new HashSet<string>(StringComparer.Ordinal); var supports=new HashSet<string>(StringComparer.Ordinal);
            foreach(var solid in definition.Solids)
            {
                if(solid==null || string.IsNullOrWhiteSpace(solid.Id)) return Reject("ARENA_INVALID_SOLID",family,null,"Every solid needs a semantic ID.");
                if(!ids.Add(solid.Id)) return Reject("ARENA_DUPLICATE_SOLID_ID",family,solid.Id,"Solid ID must be unique.");
                if(!Finite(solid.Position)||!Finite(solid.Size)||solid.Size.x<=0||solid.Size.y<=0||solid.Size.z<=0)
                    return Reject("ARENA_INVALID_SOLID_GEOMETRY",family,solid.Id,"Solid bounds must be finite and positive.");
                if(solid.Layer!=ProvingArena.WorldLayer && solid.Layer!=ProvingArena.MovementOnlyLayer)
                    return Reject("ARENA_INVALID_COLLISION_LAYER",family,solid.Id,"Only canonical world or movement-only collision layers are allowed.");
                if(!Finite(solid.Rotation.eulerAngles)) return Reject("ARENA_INVALID_SOLID_ROTATION",family,solid.Id,"Solid rotation must be finite.");
                if(!string.IsNullOrEmpty(solid.Support)) supports.Add(solid.Support);
                if(solid.Material=="floor" && string.IsNullOrWhiteSpace(solid.Support)) return Reject("ARENA_MISSING_FLOOR_SUPPORT",family,solid.Id,"Walkable floor needs a named support.");
            }
            if(supports.Count==0) return Reject("ARENA_MISSING_SUPPORTS",family,null,"At least one named support is required.");
            for(int i=0;i<definition.Spawns.Length;i++) if(!OnNamedFloor(definition.Spawns[i],definition.Solids,supports)) return Reject("ARENA_INVALID_SPAWN",family,"spawn:"+i,"Spawn must resolve to a named floor support.");
            for(int i=0;i<definition.RouteAnchors.Length;i++) if(!OnNamedFloor(definition.RouteAnchors[i],definition.Solids,supports)) return Reject("ARENA_INVALID_ROUTE_ANCHOR",family,"route-anchor:"+i,"Route anchor must resolve to a named floor support.");
            if(!OnNamedFloor(definition.ArmorPickupAnchor,definition.Solids,supports)) return Reject("ARENA_INVALID_ARMOR_PICKUP_ANCHOR",family,"armor-pickup","Armor pickup anchor must resolve to a named floor support.");

            ids.Clear(); foreach(var region in definition.SpawnRegions)
            {
                if(region==null || string.IsNullOrWhiteSpace(region.Id) || !ids.Add(region.Id)) return Reject("ARENA_INVALID_SPAWN_REGION",family,region?.Id,"Spawn region ID must be unique.");
                if(!Finite(region.Center)||!Finite(region.Size)||region.Size.x<=0||region.Size.z<=0||region.Size.y<0) return Reject("ARENA_INVALID_SPAWN_REGION",family,region.Id,"Spawn region bounds must be finite and non-degenerate.");
                if(string.IsNullOrWhiteSpace(region.Support)||!supports.Contains(region.Support)) return Reject("ARENA_UNKNOWN_SPAWN_SUPPORT",family,region.Id,"Spawn region support is not declared.");
            }
            if(definition.Transitions.Length<2) return Reject("ARENA_TOO_FEW_TRANSITIONS",family,null,"At least two declared transitions are required.");
            ids.Clear(); foreach(var transition in definition.Transitions)
            {
                if(transition==null||string.IsNullOrWhiteSpace(transition.Id)||!ids.Add(transition.Id)) return Reject("ARENA_INVALID_TRANSITION",family,transition?.Id,"Transition ID must be unique.");
                if(!supports.Contains(transition.Id)||!supports.Contains(transition.LowerSupport)||!supports.Contains(transition.UpperSupport)||transition.LowerSupport==transition.UpperSupport) return Reject("ARENA_INVALID_TRANSITION_SUPPORT",family,transition.Id,"Transition and two distinct endpoint supports must be declared.");
                if(transition.OrderedFeet==null||transition.OrderedFeet.Length<2) return Reject("ARENA_INVALID_TRANSITION_FEET",family,transition.Id,"Transition needs ordered feet.");
                if(!OnNamedFloor(transition.OrderedFeet[0],definition.Solids,supports,transition.LowerSupport)||!OnNamedFloor(transition.OrderedFeet[transition.OrderedFeet.Length-1],definition.Solids,supports,transition.UpperSupport)) return Reject("ARENA_INVALID_TRANSITION_ENDPOINT",family,transition.Id,"Transition feet must start and end on their declared supports.");
                float direction=0f, maxSegment=Mathf.Sqrt(Mathf.Pow(profile.Get("fixture.rampLength"),2)+Mathf.Pow(profile.Get("fixture.upperFloorHeight")-profile.Get("fixture.floorHeight"),2))+SurfaceTolerance;
                for(int i=0;i<transition.OrderedFeet.Length;i++)
                {
                    if(!Finite(transition.OrderedFeet[i])) return Reject("ARENA_INVALID_TRANSITION_FEET",family,transition.Id,"Transition feet must be finite.");
                    if(i==0) continue; var delta=transition.OrderedFeet[i]-transition.OrderedFeet[i-1];
                    if(delta.sqrMagnitude<=Mathf.Epsilon||delta.magnitude>maxSegment) return Reject("ARENA_DISCONTINUOUS_TRANSITION",family,transition.Id,"Transition feet exceed the profile traversal bound.");
                    float sign=Mathf.Sign(delta.y); if(sign!=0f){if(direction==0f)direction=sign;else if(sign!=direction)return Reject("ARENA_UNORDERED_TRANSITION",family,transition.Id,"Transition feet must be vertically monotonic.");}
                }
                if(direction==0f) return Reject("ARENA_UNORDERED_TRANSITION",family,transition.Id,"Transition feet must change support height.");
            }
            return ArenaValidationResult.Accept();
        }
        static ArenaValidationResult Reject(string code,string family,string element,string message) => ArenaValidationResult.Reject(code,family,element,message);
        static bool OnNamedFloor(Vector3 point,ArenaSolid[] solids,HashSet<string> supports,string requiredSupport=null)
        {
            if(!Finite(point)) return false;
            foreach(var solid in solids)
            {
                if(solid==null||solid.Material!="floor"||string.IsNullOrEmpty(solid.Support)||!supports.Contains(solid.Support)||(requiredSupport!=null&&solid.Support!=requiredSupport)) continue;
                if(Mathf.Abs(point.y-(solid.Position.y+solid.Size.y*.5f))>SurfaceTolerance) continue;
                if(Mathf.Abs(point.x-solid.Position.x)<=solid.Size.x*.5f+SurfaceTolerance&&Mathf.Abs(point.z-solid.Position.z)<=solid.Size.z*.5f+SurfaceTolerance) return true;
            }
            return false;
        }
        static bool Finite(Vector3 value) => !float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);
    }
}

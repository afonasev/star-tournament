using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public static class FiringRangeCatalog
    {
        public const string Id="firing-range-v1", Revision="1", Identity=Id+"@"+Revision;
        // Fixed topology/count is the approved authored layout; all spatial values are exported to Lab metadata.
        public const int TargetCount=12;
        public static ArenaDefinition Build()
        {
            var solids=new List<ArenaSolid>();
            void Box(string id,Vector3 pos,Vector3 size,string material="wall",string support=null)=>solids.Add(new ArenaSolid{Id=id,Position=pos,Size=size,Material=material,Support=support,Rotation=Quaternion.identity,Layer=ProvingArena.WorldLayer});
            Box("range-floor",new Vector3(0,-.2f,0),new Vector3(36,.4f,48),"floor","support:range");
            Box("range-west",new Vector3(-18.25f,4,0),new Vector3(.5f,8,49));
            Box("range-east",new Vector3(18.25f,4,0),new Vector3(.5f,8,49));
            Box("range-backstop",new Vector3(0,4,24.25f),new Vector3(36,8,.5f));
            Box("range-entry",new Vector3(0,4,-24.25f),new Vector3(36,8,.5f));
            Box("range-ceiling",new Vector3(0,8.2f,0),new Vector3(36,.4f,48));
            var spawns=new[]{new Vector3(-9,0,-20),new Vector3(-3,0,-20),new Vector3(3,0,-20),new Vector3(9,0,-20)};
            var targets=new List<Vector3>();foreach(float z in new[]{-8f,4f,16f})foreach(float x in new[]{-8f,-2f,4f,10f})targets.Add(new Vector3(x,0,z));
            var pickups=Enum.GetValues(typeof(ArenaPickupKind)).Cast<ArenaPickupKind>().Select((kind,i)=>new ArenaPickupDefinition{Id="range-"+kind.ToString().ToLowerInvariant(),Kind=kind,Support="support:range",Anchor=new Vector3(-16,0,-16+i*4)}).ToArray();
            return new ArenaDefinition{MapId=Id,Revision=Revision,Identity=Identity,ProfileFingerprint="authored:"+Identity,Solids=solids.ToArray(),Spawns=spawns,
                TrainingTargets=targets.ToArray(),Pickups=pickups,RouteAnchors=new[]{spawns[0],targets[TargetCount-1]},Transitions=Array.Empty<NativeNavigationTransition>(),
                SpawnRegions=spawns.Select((p,i)=>new ArenaSpawnRegion{Id="range-player-"+i,Center=p,Size=new Vector3(2,0,2),Support="support:range"}).ToArray()};
        }
        public static ProvingProfile AuthoringProfile()=>ProvingProfile.CreateFiringRangeAuthoring(Build());
        public static NativeMatchComposition Composition(int humans,Color[] colors=null)
        {
            if(humans<1||humans>4)throw new ArgumentOutOfRangeException(nameof(humans));
            colors=colors??NativeParticipantColors.Read();
            var metadata=Enumerable.Range(0,humans).Select(p=>new NativeParticipantInfo(NativeParticipantKind.LocalHuman,"Игрок "+(p+1),colors[p]))
                .Concat(Enumerable.Range(0,TargetCount).Select(p=>new NativeParticipantInfo(NativeParticipantKind.TrainingDummy,"Мишень "+(p+1),new Color32(196,123,80,255)))).ToArray();
            return new NativeMatchComposition(new NativeMatchRoster(NativeMatchMode.Training,new NativeTeam[metadata.Length]),metadata,Enumerable.Range(0,humans).ToArray());
        }
        public static Vector3[] Positions(ArenaDefinition definition,int humans)
        {
            if(definition.MapId!=Id||humans<1||humans>4||definition.TrainingTargets?.Length!=TargetCount)throw new ArgumentException("Invalid frozen training layout");
            return definition.Spawns.Take(humans).Concat(definition.TrainingTargets).ToArray();
        }
    }
}

using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class LunarLaboratoryPhysicsTests
    {
        [UnityTest] public IEnumerator EveryLabelIsCentredAndBackedByItsSurface()
        {
            var root=new GameObject("lunar-sign-audit");var movement=ProvingProfile.CreateDefault();var arena=root.AddComponent<ProvingArena>();
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(LunarLaboratoryCatalog.Id,movement),movement);yield return null;Physics.SyncTransforms();
                var labels=root.GetComponentsInChildren<TextMesh>();Assert.That(labels.Length,Is.EqualTo(30),"Audit must cover every wall and pickup label");
                foreach(var label in labels)
                {
                    var tr=label.transform;var bounds=label.GetComponent<MeshRenderer>().localBounds;var centre=tr.TransformPoint(bounds.center);var normal=-tr.forward;
                    Assert.That(bounds.size.x*bounds.size.y,Is.GreaterThan(0),label.text);
                    bool floor=normal.y>.9f;Vector3 expected;
                    if(floor)
                    {
                        var pickup=arena.Definition.Pickups.Single(p=>p.Id==label.text);expected=pickup.Anchor+new Vector3(0,pickup.Anchor.y==0?.064f:.010f,-.9f);
                    }
                    else
                    {
                        string wallId;float offset=0;
                        if(label.text.StartsWith("N 01"))wallId=centre.y<4.5f?"n-ground-left":"n-upper-cap-1";
                        else if(label.text.StartsWith("S 02"))wallId=centre.y<4.5f?"s-ground-left":"s-upper-cap-0";
                        else if(label.text.StartsWith("LUNA"))wallId="s-upper-cap-1";
                        else if(label.text.StartsWith("A /"))wallId="lab-front-inner--1";
                        else if(label.text.StartsWith("B /"))wallId="lab-front-inner-1";
                        else if(label.text=="H +"||label.text=="V >>")wallId="divider-base";
                        else
                        {
                            wallId=(label.text.StartsWith("W")?"w":"e")+"-annex-back";
                            offset=label.text.Contains("\n")?(centre.y<4.5f?-1.125f:2.25f):-2.25f;
                        }
                        var wall=arena.Definition.Solids.Single(s=>s.Id==wallId);
                        expected=wall.Position+Vector3.Scale(wall.Size,normal)*.5f+normal*.022f+Vector3.up*offset;
                    }
                    Assert.That(Vector3.Distance(centre,expected),Is.LessThan(.003f),"Glyph centre: "+label.text);
                    // Actual physics under a 3x3 grid catches text hanging over doors and wall edges.
                    foreach(int x in new[]{-1,0,1})foreach(int y in new[]{-1,0,1})
                    {
                        var point=tr.TransformPoint(bounds.center+new Vector3(x*bounds.extents.x,y*bounds.extents.y,0));
                        Assert.That(Physics.Raycast(point+normal*.05f,-normal,out var hit,floor?.13f:.09f,(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer)),Is.True,"Unsupported glyph: "+label.text+" at "+point);
                        Assert.That(Vector3.Dot(hit.normal,normal),Is.GreaterThan(.99f),"Wrong backing face: "+label.text);
                    }
                }
            }
            finally {Object.Destroy(root);}yield return null;
        }
        [UnityTest] public IEnumerator InteriorVolumesAndLampPostsBlockRealCapsules()
        {
            var root=new GameObject("lunar-f6-collision");var movement=ProvingProfile.CreateDefault();var arena=root.AddComponent<ProvingArena>();
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(LunarLaboratoryCatalog.Id,movement),movement);yield return null;Physics.SyncTransforms();
                foreach(var post in arena.Definition.Solids.Where(s=>s.Id.StartsWith("lamp-")))
                {
                    var start=post.Position+new Vector3(-1,-2,0);var go=new GameObject("post-walker");go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,start,arena);
                    var state=motor.State;state.Yaw=90;motor.RestoreState(state);
                    for(int tick=0;tick<90;tick++){motor.Tick(new LocalAction{Move=Vector2.up},1/60f);Physics.SyncTransforms();}
                    Assert.That(motor.State.Position.x,Is.LessThan(post.Position.x-.2f),post.Id);Object.DestroyImmediate(go);
                }
                foreach(int side in new[]{-1,1})
                {
                    Assert.That(Physics.Raycast(new Vector3(side*11,1,-1.75f),Vector3.right*side,out var hit,2,ProvingArena.ShotMask),Is.True,"under upper march");
                    Assert.That(hit.collider.name,Does.Contain("infill"));
                    var wall=arena.Definition.Solids.Single(s=>s.Id==(side<0?"w":"e")+"-ground--1");
                    float plane=wall.Position.x-side*wall.Size.x*.5f;
                    Assert.That(hit.point.x,Is.EqualTo(plane).Within(.001f),"enclosure must be flush with adjacent wall");
                    // Cover the full neighbouring wall height and tread-side gaps; the slab edge above
                    // is inset on every wall and is not part of the vertical wall plane.
                    foreach(float z in new[]{-3.4f,-1.75f,-.12f})foreach(float y in new[]{.1f,2f,wall.Position.y+wall.Size.y*.5f-.02f})
                    {
                        Assert.That(Physics.Raycast(new Vector3(side*11,y,z),Vector3.right*side,out var face,2,ProvingArena.ShotMask),Is.True);
                        Assert.That(face.point.x,Is.EqualTo(plane).Within(.001f),"continuous front plane");
                    }
                }
                foreach(var header in arena.Definition.Solids.Where(s=>s.Id.Contains("header")&&!s.Id.Contains("open-header")))
                {
                    var c=header.Position;var across=header.Size.x>header.Size.z?Vector3.forward:Vector3.right;
                    Assert.That(Physics.Raycast(c-across,across,2,ProvingArena.ShotMask),Is.True,header.Id);
                }
                // Main open areas must admit a capsule at floor level after the cargo reduction.
                foreach(int side in new[]{-1,1})for(int x=-3;x<=3;x+=3)for(int z=5;z<=9;z+=2)
                    Assert.That(Physics.CheckCapsule(new Vector3(x,.6f,side*z),new Vector3(x,1.4f,side*z),.35f,ProvingArena.ShotMask),Is.False,"open centre");
            }
            finally {Object.Destroy(root);}yield return null;
        }
        [UnityTest] public IEnumerator LowerFloorBypassesBesideBothStairsAdmitActualCapsule()
        {
            var root=new GameObject("lunar-f10-bypass");var movement=ProvingProfile.CreateDefault();var arena=root.AddComponent<ProvingArena>();
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(LunarLaboratoryCatalog.Id,movement),movement);yield return null;Physics.SyncTransforms();
                foreach(int side in new[]{-1,1})foreach(bool reverse in new[]{false,true})
                {
                    var route=new[]{new Vector3(side*6.5f,.02f,-side*4.5f),new Vector3(side*10.6f,.02f,-side*4.5f),new Vector3(side*10.6f,.02f,side*4.5f),new Vector3(side*6.5f,.02f,side*4.5f)};
                    if(reverse)System.Array.Reverse(route);
                    var go=new GameObject("bypass-walker");go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();
                    var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,route[0],arena);
                    foreach(var target in route.Skip(1))
                    {
                        // Steer through the corner while preserving real motor inertia and collisions.
                        // A fixed heading after a turn drifts sideways, even in an empty room.
                        for(int tick=0;tick<180;tick++)
                        {
                            var delta=target-motor.State.Position;delta.y=0;if(delta.magnitude<=.1f)break;
                            var state=motor.State;state.Yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;motor.RestoreState(state);
                            motor.Tick(new LocalAction{Move=Vector2.up},1/60f);Physics.SyncTransforms();
                        }
                        var remaining=target-motor.State.Position;remaining.y=0;
                        Assert.That(remaining.magnitude,Is.LessThan(.2f),"bypass must include both turns, side="+side+" reverse="+reverse+" target="+target+" actual="+motor.State.Position);
                    }
                    Object.DestroyImmediate(go);
                }
            }
            finally {Object.Destroy(root);}yield return null;
        }
        [UnityTest] public IEnumerator RoutesStairsGlassAndSpawns()
        {
            var root=new GameObject("lunar-physics");var movement=ProvingProfile.CreateDefault();var nav=ProvingProfile.CreateNavigationDefault();var arena=root.AddComponent<ProvingArena>();
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(LunarLaboratoryCatalog.Id,movement),movement);yield return null;
                var selector=new SafeSpawnSelector(root.scene.GetPhysicsScene(),arena,movement,ProvingProfile.CreateNativeCombatDefault());
                for(int a=0;a<8;a++){Assert.That(selector.Valid(arena.Spawns[a],out _),Is.True,"invalid S"+(a+1));for(int b=a+1;b<8;b++)Assert.That(selector.BodyVisible(arena.Spawns[a],arena.Spawns[b])||selector.BodyVisible(arena.Spawns[b],arena.Spawns[a]),Is.False,"visible S"+(a+1)+" S"+(b+1));}
                Assert.That(selector.TryInitial(NativeMatchRoster.Ffa(8),ProvingProfile.CreateTeamDefault().Get("spawn.initialOpponentSeparation"),out _),Is.True,selector.InitialFailure);
                var provider=new NativeNavigationProvider(arena,movement,nav);
                foreach(var from in arena.Spawns)foreach(var to in arena.Definition.Pickups.Select(p=>p.Anchor).Concat(arena.Spawns))
                    Assert.That(provider.TryRoute(from,to,out _,out var failure),Is.True,from+" -> "+to+" "+failure);
                // Both portals of each downstairs storage room must remain real bot passages.
                foreach(int side in new[]{-1,1})foreach(bool across in new[]{false,true})
                {
                    var start=across?new Vector3(side*5,.02f,-side*8):new Vector3(side*7.25f,.02f,-side*5);
                    var end=across?new Vector3(side*7,.02f,-side*8):new Vector3(side*7.25f,.02f,-side*7.5f);
                    Assert.That(provider.TryRoute(start,end,out var route,out var failure),Is.True,"storage portal "+side+" "+across+" "+failure);
                    float length=0;var previous=start;foreach(var point in route){length+=Vector3.Distance(previous,point.Position);previous=point.Position;}
                    Assert.That(length,Is.LessThan(5),"portal must not detour through the other door");
                }
                foreach(var t in arena.Definition.Transitions)foreach(int dir in new[]{-1,1})foreach(int lane in new[]{-1,0,1})
                {
                    var lower=t.OrderedFeet.First();var upper=t.OrderedFeet.Last();var f=upper-lower;f.y=0;f.Normalize();var across=Vector3.Cross(Vector3.up,f)*lane*movement.Get("player.capsule.radius");
                    var start=(dir>0?lower-f*.65f:upper+f*.65f)+across;var goal=(dir>0?upper+f*.65f:lower-f*.65f)+across;
                    var go=new GameObject("walker");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,start,arena);
                    var bot=new NativeBotNavigation(provider,nav);bot.SetStaticGoal(goal);var own=new NativeBotKnowledge{Alive=true,OwnLife=1};
                    for(int tick=0;tick<1200&&bot.Status!=NativeNavigationStatus.Arrived&&bot.Status!=NativeNavigationStatus.Blocked;tick++)
                    {var action=bot.Tick(tick/60d,motor.State,own);motor.Tick(action,1/60f);Physics.SyncTransforms();}
                    Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Arrived),t.Id+" "+dir+" "+lane+" "+bot.Capture().Failure+" "+motor.State.Position);Object.DestroyImmediate(go);
                }
                Assert.That(selector.BodyVisible(new Vector3(0,.02f,2),new Vector3(0,.02f,-2)),Is.True,"glass passes optical sight");
                Assert.That(Physics.Raycast(new Vector3(0,1.7f,2),Vector3.back,out var hit,4,ProvingArena.ShotMask),Is.True);Assert.That(hit.collider.name,Is.EqualTo("divider-glass"));
                Assert.That(Physics.Raycast(new Vector3(8,6.3f,11),Vector3.forward,2,ProvingArena.ShotMask),Is.True,"armored north window");
                Assert.That(Physics.Raycast(new Vector3(11,6.3f,7),Vector3.right,2,ProvingArena.ShotMask),Is.False,"authored broken east window");
                Assert.That(Physics.Raycast(new Vector3(-2,6.2f,10),Vector3.right,4,ProvingArena.ShotMask),Is.True,"S7/S8 opaque common wall");
                // Real controller jumps through the authored broken window and over the balcony guard.
                foreach(bool window in new[]{false,true})
                {
                    var go=new GameObject("jumper");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var m=go.AddComponent<CharacterMotor>();m.Initialize(movement,window?new Vector3(10,4.5f,-10.5f):new Vector3(0,4.5f,13.4f),arena);
                    var pose=m.State;pose.Yaw=window?90:0;m.RestoreState(pose);
                    for(int tick=0;tick<(window?65:150);tick++){m.Tick(new LocalAction{Move=tick>2?Vector2.up:Vector2.zero,Jump=tick==3},1/60f);Physics.SyncTransforms();}
                    Assert.That(window?m.State.Position.x:m.State.Position.z,Is.GreaterThan(window?12.5f:16),"jump passage "+window);
                    if(window)Assert.That(m.State.Position.y,Is.EqualTo(4.5f).Within(.05f),"window exits onto balcony");else Assert.That(m.State.Position.y,Is.LessThan(.2f),"balcony jump lands in courtyard");Object.DestroyImmediate(go);
                }
                var art=root.GetComponentInChildren<LunarLaboratoryPresentation>();Assert.That(art.LightCount,Is.GreaterThan(20));Assert.That(art.GetComponentsInChildren<Collider>(),Is.Empty);
                arena.Build(CombatBowlCatalog.Freeze(movement),movement);Assert.That(root.GetComponentInChildren<LunarLaboratoryPresentation>(),Is.Null);
            }
            finally {Object.Destroy(root);}yield return null;
        }
    }
}

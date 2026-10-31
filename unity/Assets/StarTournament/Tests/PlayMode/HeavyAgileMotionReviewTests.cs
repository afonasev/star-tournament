#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class HeavyAgileMotionReviewTests
    {
        [UnityTest,Explicit("Focused graphical motion evidence; set STAR_TOURNAMENT_HEAVY_EVIDENCE")]
        public IEnumerator CaptureHeavyAgileMovement()
        {
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_HEAVY_EVIDENCE");
            Assert.That(output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(output);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            var studio=new GameObject("heavy-agile-review");
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Trooper/TrooperBody.prefab"),studio.transform);
            var visual=root.AddComponent<TrooperVisual>();var profile=ProvingProfile.CreateTrooperDefault();
            visual.Initialize(TrooperVisual.ClipNames.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/StarTournament/Trooper/"+n+".anim")).ToArray(),profile);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())renderer.gameObject.layer=30;
            TrooperIdentityPresentation.ApplyBody(root,new Color(.7f,.1f,.38f));
            var camera=new GameObject("review-camera").AddComponent<Camera>();camera.transform.SetParent(studio.transform);camera.enabled=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.19f,.21f,.24f);camera.cullingMask=1<<30;camera.fieldOfView=35;
            foreach(var angle in new[]{new Vector3(35,-30,0),new Vector3(25,145,0)})
            {
                var light=new GameObject("review-fill").AddComponent<Light>();light.transform.SetParent(studio.transform);light.type=LightType.Directional;
                light.intensity=.9f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(angle);
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.SetParent(studio.transform);floor.layer=30;
            floor.transform.localScale=Vector3.one*12;Object.Destroy(floor.GetComponent<Collider>());
            var rt=new RenderTexture(800,900,24);rt.Create();var pixels=new Texture2D(800,900,TextureFormat.RGB24,false);
            var bones=root.GetComponentsInChildren<Transform>();var feet=new[]{bones.Single(t=>t.name=="LeftFoot"),bones.Single(t=>t.name=="RightFoot")};
            var gait=typeof(TrooperVisual).GetField("gait",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(visual);
            var swing=gait.GetType().GetProperty("SwingFoot");var anchor=gait.GetType().GetMethod("Anchor");
            float maxSlip=0;var oldActive=RenderTexture.active;
            void Snapshot(string name)
            {
                    camera.targetTexture=rt;
                    if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});else camera.Render();
                    RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,800,900),0,0);pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output,name),pixels.EncodeToPNG());
            }
            try
            {
                yield return null;visual.Render(0,Vector3.zero,100,1);
                for(int frame=0;frame<240;frame++)
                {
                    float t=frame/24f;Vector3 velocity=Vector3.zero;bool grounded=true;
                    if(t>=.5f&&t<3)velocity=Vector3.forward*(6*Mathf.Clamp01((t-.5f)/.5f));
                    else if(t>=3&&t<3.5f)velocity=Vector3.forward*(6*(1-(t-3)/.5f));
                    else if(t>=4&&t<5.3f)velocity=Vector3.right*3;
                    else if(t>=5.3f&&t<6.5f)velocity=Vector3.left*3;
                    else if(t>=7&&t<8){root.transform.rotation=Quaternion.Euler(0,(t-7)*100,0);}
                    else if(t>=8&&t<9){grounded=false;velocity=Vector3.up*(4-8*(t-8));}
                    root.transform.position+=velocity/24;
                    if(grounded)root.transform.position=new Vector3(root.transform.position.x,0,root.transform.position.z);
                    var motor=root.transform.position;visual.Render(t,velocity,100,1,grounded);
                    Assert.That(root.transform.position,Is.EqualTo(motor));
                    if(grounded)
                        for(int foot=0;foot<2;foot++)if(foot!=(int)swing.GetValue(gait))maxSlip=Mathf.Max(maxSlip,Vector3.Distance(feet[foot].position,(Vector3)anchor.Invoke(gait,new object[]{foot})));
                    camera.transform.position=root.transform.position+new Vector3(2.1f,1.55f,3.7f);camera.transform.LookAt(root.transform.position+Vector3.up*.88f);
                    floor.transform.position=new Vector3(root.transform.position.x,0,root.transform.position.z);
                    Snapshot($"frame-{frame:D3}.png");
                    if(frame%24==0)yield return null;
                }
                foreach(var view in new[]{("front",0f),("side",90f),("back",180f)})
                {
                    root.transform.rotation=Quaternion.Euler(0,view.Item2,0);visual.ResetPose(10);visual.Render(10,Vector3.zero,100,1);
                    Snapshot("native-"+view.Item1+".png");
                }
                foreach(var color in new[]{("blue",new Color(.06f,.30f,.85f)),("orange",new Color(.9f,.23f,.04f))})
                {
                    TrooperIdentityPresentation.ApplyBody(root,color.Item2);
                    foreach(var facing in new[]{("front",0f),("back",180f)})
                    {
                        root.transform.rotation=Quaternion.Euler(0,facing.Item2,0);visual.ResetPose(10);visual.Render(10,Vector3.zero,100,1);
                        Snapshot("identity-"+color.Item1+"-"+facing.Item1+".png");
                    }
                }
                File.WriteAllText(Path.Combine(output,"motion-metrics.txt"),"max support slip metres="+maxSlip+"\nFrames=240 at authored 24 Hz; renderer-only trajectory, not performance or human acceptance.\n");
                Assert.That(maxSlip,Is.LessThan(.002f),"Support remains planted through acceleration, strafing, reversal, pivot and landing");
            }
            finally{RenderTexture.active=oldActive;camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(pixels);Object.Destroy(studio);}
        }
    }
}
#endif

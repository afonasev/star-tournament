using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class WellSignPlacementTests
    {
        [UnityTest] public IEnumerator EverySignFitsAnOpaqueFaceAtEverySupportedScale()
        {
            var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-storeys/signs"));Directory.CreateDirectory(directory);
            var audit=new List<string>();var failures=new List<string>();
            foreach(float scale in new[]{.5f,1f,2f})
            {
                var root=new GameObject("Well sign audit");var movement=ProvingProfile.CreateDefault();var art=ProvingProfile.CreateWellDefault();art.Set("well.signScale",scale);
                var arenaGo=new GameObject("Arena");arenaGo.transform.SetParent(root.transform);var arena=arenaGo.AddComponent<ProvingArena>();
                var cameraGo=new GameObject("Audit camera");cameraGo.transform.SetParent(root.transform);var camera=cameraGo.AddComponent<Camera>();camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);
                var lightGo=new GameObject("Audit light");lightGo.transform.SetParent(root.transform);WellArenaPresentation.ConfigureDaylight(lightGo.AddComponent<Light>(),art);
                var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;
                try
                {
                    arena.Build(AuthoredArenaCatalog.Freeze(WellArenaCatalog.Id,movement),movement,art);yield return null;yield return null;
                    var skin=arena.GetComponentInChildren<WellArenaPresentation>();var signs=skin.GetComponentsInChildren<TextMesh>();
                    Assert.That(signs.Length,Is.EqualTo(30),"Audit eight wall signs and two floor numbers on every storey");
                    Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(JsonUtility.ToJson(WellArenaCatalog.Build())),"Sign placement cannot change gameplay authoring");
                    Assert.That(skin.GetComponentsInChildren<Collider>(),Is.Empty);
                    int index=0;
                    foreach(var sign in signs)
                    {
                        var renderer=sign.GetComponent<MeshRenderer>();var bounds=renderer.localBounds;var normal=-sign.transform.forward;
                        Assert.That(bounds.size.x,Is.GreaterThan(.01f),sign.text);Assert.That(bounds.size.y,Is.GreaterThan(.01f),sign.text);
                        var centre=sign.transform.TransformPoint(bounds.center);var world=renderer.bounds;
                        // Dense sampling of the entire text rectangle, not just glyph centres or a chosen anchor.
                        // A 3.2 m portal or a perpendicular room wall cannot be hidden between samples.
                        for(int x=0;x<=40;x++)for(int y=0;y<=6;y++)
                        {
                            var point=sign.transform.TransformPoint(new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x/40f),Mathf.Lerp(bounds.min.y,bounds.max.y,y/6f),bounds.center.z));
                            var behind=point-normal*.03f;
                            bool backed=arena.Definition.Solids.Any(s=>(s.Material=="wall"||Mathf.Abs(normal.y)>.9f&&s.Material=="floor")&&new Bounds(s.Position,s.Size).Contains(behind));
                            bool buried=arena.Definition.Solids.Any(s=>s.Material!="glass"&&new Bounds(s.Position,s.Size).Contains(point+normal*.005f));
                            if(!backed||buried){failures.Add("scale="+scale+" #"+index+" "+sign.text+" at "+point.ToString("F3")+" backing="+backed+" buried="+buried);break;}
                        }
                        audit.Add("scale="+scale+" #"+index+" "+sign.text+" centre="+centre.ToString("F3")+" size="+world.size.ToString("F3"));
                        if(scale==1)
                        {
                            float width=bounds.size.x*sign.transform.lossyScale.x,height=bounds.size.y*sign.transform.lossyScale.y;
                            float distance=Mathf.Max(3.8f,width*.75f,height*2);
                            cameraGo.transform.position=centre+normal*distance;cameraGo.transform.LookAt(centre);camera.Render();yield return null;
                            var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                            try{image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,"sign-"+index.ToString("D2")+".png"),image.EncodeToPNG());}
                            finally{RenderTexture.active=previous;Object.Destroy(image);}
                        }
                        index++;
                    }
                }
                finally{camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(root);}yield return null;
            }
            File.WriteAllLines(Path.Combine(directory,"sign-audit.txt"),audit.Concat(failures));
            Assert.That(failures,Is.Empty,string.Join("\n",failures));Assert.That(audit.Count,Is.EqualTo(90));
        }
    }
}

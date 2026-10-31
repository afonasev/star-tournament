using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class FirstMapLoadingVisualTests
    {
        [Serializable] sealed class MeshData {public Vector3[] Vertices,Normals;public Vector4[] Tangents;public Color[] Colors;public Vector2[] Uv;public int[] Triangles;public int[] Submeshes;}
        [UnityTest,Explicit("Focused GPU capture; STAR_TOURNAMENT_QA_FIRST_LOADING_RENDER required")]
        public IEnumerator CaptureFreshGeometryAndViews()
        {
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_FIRST_LOADING_RENDER");Assert.That(output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(output);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            foreach(var map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id})
            {
                var root=new GameObject("fresh-visual");var arena=root.AddComponent<ProvingArena>();var eye=new GameObject("frozen-camera");var camera=eye.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.farClipPlane=300;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);
                var lamp=new GameObject("frozen-daylight");var light=lamp.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.shadows=LightShadows.Soft;lamp.transform.rotation=Quaternion.Euler(45,-30,0);
                var target=new RenderTexture(960,540,24,RenderTextureFormat.DefaultHDR);camera.targetTexture=target;var old=RenderTexture.active;
                try
                {
                    var movement=ProvingProfile.CreateDefault();arena.Build(AuthoredArenaCatalog.Freeze(map,movement),movement);
                    foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                    if(map==WellArenaCatalog.Id)WellArenaPresentation.ConfigureDaylight(light,ProvingProfile.CreateWellDefault());
                    var hashes=new List<string>();var meshes=new HashSet<Mesh>();long vertices=0,triangles=0;
                    foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var m=f.sharedMesh;if(!m||f.GetComponent<TextMesh>()||!meshes.Add(m))continue;vertices+=m.vertexCount;triangles+=Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i));
                        if(!m.isReadable){hashes.Add(m.name+":nonreadable:"+m.vertexCount+":"+m.bounds);continue;}
                        var data=new MeshData{Vertices=m.vertices,Normals=m.normals,Tangents=m.tangents,Colors=m.colors,Uv=m.uv,Triangles=m.triangles,Submeshes=Enumerable.Range(0,m.subMeshCount).Select(i=>(int)m.GetIndexCount(i)).ToArray()};
                        using(var sha=SHA256.Create())hashes.Add(m.name+":"+BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)))));
                    }
                    hashes.Sort(StringComparer.Ordinal);var rs=root.GetComponentsInChildren<Renderer>(true);var mats=new HashSet<Material>(rs.SelectMany(r=>r.sharedMaterials));var textures=new HashSet<Texture>();foreach(var m in mats)if(m)foreach(var n in m.GetTexturePropertyNames()){var t=m.GetTexture(n);if(t)textures.Add(t);}
                    File.WriteAllText(Path.Combine(output,map+"-geometry.txt"),$"definition={JsonUtility.ToJson(arena.Definition)}\nvertices={vertices}\nindices={triangles}\nrenderers={rs.Length}\nmaterials={mats.Count}\ntextures={textures.Count}\nlights={root.GetComponentsInChildren<Light>(true).Length}\nlods={root.GetComponentsInChildren<LODGroup>(true).Length}\n"+string.Join("\n",hashes));
                    for(int view=0;view<(map==WellArenaCatalog.Id?2:1);view++)
                    {
                        eye.transform.position=map==CombatBowlCatalog.Id?new Vector3(0,1.7f,-8):map==IndustrialTunnelsCatalog.Id?arena.Spawns[0]+Vector3.up*1.7f:map==LunarLaboratoryCatalog.Id?new Vector3(0,1.7f,-9):view==0?new Vector3(7,1.7f,-7):new Vector3(20,1.7f,21);
                        eye.transform.LookAt(map==CombatBowlCatalog.Id?new Vector3(0,1.7f,8):map==IndustrialTunnelsCatalog.Id?new Vector3(-16,1.1f,13):map==LunarLaboratoryCatalog.Id?new Vector3(0,1.7f,9):view==0?new Vector3(-5,12,5):new Vector3(14,2.7f,16));
                        for(int sample=0;sample<2;sample++)
                        {
                            yield return null;camera.Render();RenderTexture.active=target;var image=new Texture2D(960,540,TextureFormat.RGB24,false);
                            try{image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,$"{map}-view{view}-{sample}.png"),image.EncodeToPNG());}finally{Object.Destroy(image);RenderTexture.active=old;}
                        }
                    }
                }
                finally{camera.targetTexture=null;RenderTexture.active=old;target.Release();Object.Destroy(target);Object.Destroy(root);Object.Destroy(eye);Object.Destroy(lamp);}yield return null;
            }
        }
    }
}

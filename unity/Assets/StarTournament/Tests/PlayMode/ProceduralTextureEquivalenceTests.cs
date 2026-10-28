using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ProceduralTextureEquivalenceTests
    {
        static readonly FieldInfo Parallel=typeof(ProvingArena).Assembly.GetType("StarTournament.ProvingGround.ArenaTextureWork").GetField("ParallelEnabled",BindingFlags.Static|BindingFlags.NonPublic);
        [UnityTest,Explicit("Focused GPU texture equivalence")]
        public IEnumerator WellTexturesMatchFrozenReferenceAcrossProfileExtremes()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));var p=ProvingProfile.CreateWellDefault();
            foreach(bool parallel in new[]{false,true})
            {
                Parallel.SetValue(null,parallel);p=ProvingProfile.CreateWellDefault();
                try
                {
                    for(int variant=0;variant<3;variant++)
                    {
                        if(variant>0){p.Set("well.surfaceGrain",variant==1?p.Descriptor("well.surfaceGrain").Minimum:p.Descriptor("well.surfaceGrain").Maximum);p.Set("well.jointContrast",variant==1?p.Descriptor("well.jointContrast").Minimum:p.Descriptor("well.jointContrast").Maximum);}
                        foreach(var kind in new[]{"ceramic","floor","floor-bio","floor-physics","floor-electronics","ceiling","metal","soil","leaf"})foreach(bool normal in new[]{false,true})
                        {
                            var expected=LegacyWellTextureReference.Texture(kind,p,normal);var actual=WellLabDetail.Texture(kind,p,normal);
                            try{Assert.That(Hash(actual),Is.EqualTo(Hash(expected)),kind+" normal="+normal+" variant="+variant+" parallel="+parallel);}
                            finally{Object.DestroyImmediate(actual);Object.DestroyImmediate(expected);}
                        }
                    }
                }
                finally{Parallel.SetValue(null,true);}
            }
            yield return null;
        }
        [UnityTest,Explicit("Focused GPU texture equivalence")]
        public IEnumerator AllMapTexturesMatchBetweenSerialAndBoundedParallelGeneration()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            foreach(var map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id})
            {
                string[] serial=null;
                foreach(bool parallel in new[]{false,true})
                {
                    var root=new GameObject("texture-equivalence");Parallel.SetValue(null,parallel);
                    try
                    {
                        var movement=ProvingProfile.CreateDefault();var arena=root.AddComponent<ProvingArena>();arena.Build(AuthoredArenaCatalog.Freeze(map,movement),movement);
                        var textures=new HashSet<Texture2D>();
                        foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                        {
                            if(r.GetComponent<TextMesh>())continue;
                            foreach(var m in r.sharedMaterials)if(m)foreach(var n in m.GetTexturePropertyNames())if(m.GetTexture(n) is Texture2D texture)textures.Add(texture);
                        }
                        var hashes=textures.Select(t=>t.name+":"+t.width+"x"+t.height+":"+Hash(t)).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
                        if(!parallel)serial=hashes;else Assert.That(hashes,Is.EqualTo(serial),map);
                    }
                    finally{Parallel.SetValue(null,true);Object.Destroy(root);}yield return null;
                }
            }
        }
        static string Hash(Texture2D source)
        {
            var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);var old=RenderTexture.active;
            var pixels=new Texture2D(source.width,source.height,TextureFormat.RGBAFloat,false,true);
            try
            {
                Graphics.Blit(source,target);RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,source.width,source.height),0,0);pixels.Apply();
                using(var stream=new MemoryStream())
                {
                    using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))foreach(var c in pixels.GetPixels()){writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a);}
                    using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray()));
                }
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(pixels);}
        }
    }
}

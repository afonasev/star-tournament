using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    static class SecondaryRenderingCostDiagnostics
    {
        static string Csv(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
        static Mesh MeshFor(Renderer renderer)
        {
            if(renderer is SkinnedMeshRenderer skinned)return skinned.sharedMesh;
            var filter=renderer.GetComponent<MeshFilter>();
            return filter?filter.sharedMesh:null;
        }
        static long RendererTriangles(Renderer renderer,Mesh mesh)
        {
            if(!mesh)return 0;
            if(renderer is MeshRenderer combined&&renderer.isPartOfStaticBatch)
            {
                int first=combined.subMeshStartIndex,count=Mathf.Min(renderer.sharedMaterials.Length,mesh.subMeshCount-first);long sum=0;
                for(int i=0;i<count;i++)sum+=(long)mesh.GetIndexCount(first+i)/3;
                return sum;
            }
            return Enumerable.Range(0,mesh.subMeshCount).Sum(i=>(long)mesh.GetIndexCount(i)/3);
        }
        public static void Write(string directory,string category,IEnumerable<Renderer> source)
            =>Write(directory,category,source,null);
        public static void Write(string directory,string category,IEnumerable<Renderer> source,IEnumerable<Material> extraMaterials)
        {
            Directory.CreateDirectory(directory);
            var renderers=source.Where(r=>r).Distinct().ToArray();
            var meshes=renderers.Select(MeshFor).Where(m=>m).GroupBy(m=>m.GetInstanceID()).Select(g=>g.First()).ToArray();
            var materials=renderers.SelectMany(r=>r.sharedMaterials).Concat(extraMaterials??Enumerable.Empty<Material>()).Where(m=>m).GroupBy(m=>m.GetInstanceID()).Select(g=>g.First()).ToArray();
            var textures=materials.SelectMany(m=>m.GetTexturePropertyNames().Select(p=>m.GetTexture(p))).Where(t=>t).GroupBy(t=>t.GetInstanceID()).Select(g=>g.First()).ToArray();
            long meshBytes=meshes.Sum(m=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m));
            long materialBytes=materials.Sum(m=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m));
            long textureBytes=textures.Sum(t=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
            File.WriteAllLines(Path.Combine(directory,category+"-renderers.csv"),new[]{"category,renderer,type,renderer_id,enabled,mesh_id,material_count,static_batch,rendered_triangles,shadow_cast,receive_shadows,light_probes,reflection_probes"}.Concat(renderers.Select(r=>
            {
                var mesh=MeshFor(r);
                return string.Join(",",new[]{category,r.name,r.GetType().Name,r.GetInstanceID().ToString(CultureInfo.InvariantCulture),(r.enabled&&r.gameObject.activeInHierarchy).ToString(),mesh?mesh.GetInstanceID().ToString(CultureInfo.InvariantCulture):"",r.sharedMaterials.Length.ToString(CultureInfo.InvariantCulture),r.isPartOfStaticBatch.ToString(),RendererTriangles(r,mesh).ToString(CultureInfo.InvariantCulture),r.shadowCastingMode.ToString(),r.receiveShadows.ToString(),r.lightProbeUsage.ToString(),r.reflectionProbeUsage.ToString()}.Select(Csv));
            })));
            File.WriteAllLines(Path.Combine(directory,category+"-meshes.csv"),new[]{"mesh_id,name,bytes,vertices,triangles,submeshes,index_format,readable,renderer_references"}.Concat(meshes.Select(m=>
                string.Join(",",new[]{m.GetInstanceID().ToString(CultureInfo.InvariantCulture),m.name,UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m).ToString(CultureInfo.InvariantCulture),m.vertexCount.ToString(CultureInfo.InvariantCulture),Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3).ToString(CultureInfo.InvariantCulture),m.subMeshCount.ToString(CultureInfo.InvariantCulture),m.indexFormat.ToString(),m.isReadable.ToString(),renderers.Count(r=>MeshFor(r)==m).ToString(CultureInfo.InvariantCulture)}.Select(Csv)))));
            File.WriteAllLines(Path.Combine(directory,category+"-materials.csv"),new[]{"material_id,name,shader,render_queue,bytes,texture_references"}.Concat(materials.Select(m=>
                string.Join(",",new[]{m.GetInstanceID().ToString(CultureInfo.InvariantCulture),m.name,m.shader?m.shader.name:"",m.renderQueue.ToString(CultureInfo.InvariantCulture),UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m).ToString(CultureInfo.InvariantCulture),m.GetTexturePropertyNames().Count(p=>m.GetTexture(p)!=null).ToString(CultureInfo.InvariantCulture)}.Select(Csv)))));
            File.WriteAllLines(Path.Combine(directory,category+"-textures.csv"),new[]{"texture_id,name,bytes,width,height,format,mipmaps,readable"}.Concat(textures.Select(t=>
            {
                var image=t as Texture2D;return string.Join(",",new[]{t.GetInstanceID().ToString(CultureInfo.InvariantCulture),t.name,UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t).ToString(CultureInfo.InvariantCulture),image?image.width.ToString(CultureInfo.InvariantCulture):"",image?image.height.ToString(CultureInfo.InvariantCulture):"",image?image.format.ToString():"",image?image.mipmapCount.ToString(CultureInfo.InvariantCulture):"",image?image.isReadable.ToString():""}.Select(Csv));
            })));
            var lodGroups=renderers.Select(r=>r.GetComponentInParent<LODGroup>()).Where(g=>g).GroupBy(g=>g.GetInstanceID()).Select(g=>g.First()).ToArray();
            File.WriteAllLines(Path.Combine(directory,category+"-summary.txt"),new[]{"category="+category,"renderer_count="+renderers.Length,"unique_mesh_count="+meshes.Length,"unique_material_count="+materials.Length,"unique_texture_count="+textures.Length,"unique_mesh_bytes="+meshBytes,"unique_material_bytes="+materialBytes,"unique_texture_bytes="+textureBytes,"unique_asset_bytes="+(meshBytes+materialBytes+textureBytes),"unique_mesh_triangles="+meshes.Sum(m=>Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3)),"lod_group_count="+lodGroups.Length,"lod_count="+lodGroups.Sum(g=>g.GetLODs().Length)});
        }
        public static void WriteParticles(string directory,string category,IEnumerable<ParticleSystem> source)
        {
            var systems=source.Where(s=>s).Distinct().ToArray();
            File.WriteAllLines(Path.Combine(directory,category+"-particles.csv"),new[]{"name,instance_id,active,particles,max_particles,renderer_mode,material,material_id,shadow_cast,receive_shadows"}.Concat(systems.Select(s=>
            {
                var r=s.GetComponent<ParticleSystemRenderer>();var main=s.main;var m=r?r.sharedMaterial:null;
                return string.Join(",",new[]{s.name,s.GetInstanceID().ToString(CultureInfo.InvariantCulture),s.gameObject.activeInHierarchy.ToString(),s.particleCount.ToString(CultureInfo.InvariantCulture),main.maxParticles.ToString(CultureInfo.InvariantCulture),r?r.renderMode.ToString():"",m?m.name:"",m?m.GetInstanceID().ToString(CultureInfo.InvariantCulture):"",r?r.shadowCastingMode.ToString():"",r?r.receiveShadows.ToString():""}.Select(Csv));
            })));
        }
        public static void Capture(Camera camera,string path,int width=1280,int height=720)
        {
            var target=new RenderTexture(width,height,24,RenderTextureFormat.DefaultHDR);target.Create();var image=new Texture2D(width,height,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=target;camera.aspect=(float)width/height;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
#if UNITY_EDITOR
                File.WriteAllLines(Path.ChangeExtension(path,".render.txt"),new[]{"resolution="+width+"x"+height,"render_submit_count=1","editor_draw_calls="+UnityEditor.UnityStats.drawCalls,"editor_triangles="+UnityEditor.UnityStats.triangles,"note=single captured frame counters; not FPS/CPU/GPU timing"});
#endif
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(image);}
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace StarTournament.ProvingGround.Editor
{
    /// <summary>All curves/materials persisted before Player build; no runtime import or curve authoring.</summary>
    public static class TrooperPreparation
    {
        const string Art="Assets/StarTournament/Art/";
        const string Output="Assets/StarTournament/Trooper/";
        public static void RefreshExistingScene()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/StarTournament/Scenes/ProvingGround.unity");
            var ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            Prepare(ground,AssetDatabase.LoadAssetAtPath<GameObject>(Art+"trooper-body-animated.glb"),AssetDatabase.LoadAssetAtPath<GameObject>(Art+"trooper-arms-animated.glb"));
            // Existing prefab GUIDs remain stable; do not serialize unrelated scene defaults.
        }
        public static void Prepare(ProvingGround ground,GameObject source,GameObject arms)
        {
            Directory.CreateDirectory(Output); AssetDatabase.Refresh();
            if(!source || !arms) throw new BuildFailedException("Run scripts/trooper/unity_build_shipping.py before Unity prepare");
            var imported=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(source)).OfType<AnimationClip>().ToArray();
            ground.TrooperClips=new AnimationClip[TrooperVisual.ClipNames.Length];
            for(int i=0;i<ground.TrooperClips.Length;i++)
            {
                string name=TrooperVisual.ClipNames[i];
                var original=imported.SingleOrDefault(c=>c.name==name);
                if(!original || original.legacy) throw new BuildFailedException("Missing nonlegacy trooper clip "+name);
                string path=Output+name+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                bool created=!clip;
                if(created) { clip=new AnimationClip(); AssetDatabase.CreateAsset(clip,path); }
                // Vector changes equipment only. Preserve the accepted persisted motion curves;
                // Blender GLB roundtrips resample them even when no animation edit was requested.
                bool vectorEquipment=source.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="vector-muzzle-0");
                if(created || !vectorEquipment) EditorUtility.CopySerialized(original,clip);
                clip.name=name;
                var settings=AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime=i<4; settings.loopBlend=false;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
                EditorUtility.SetDirty(clip); ground.TrooperClips[i]=clip;
            }
            var bodyMaterials=source.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
            var materials=source.GetComponentsInChildren<Renderer>(true).Concat(arms.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Where(m=>m).GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
            // Blender appends .001 to a repeated Strata material when the F3 hands
            // are imported before the new weapon. Both views must share the body
            // material subasset, including after a fresh Player build.
            foreach(var key in materials.Keys.ToArray())
            {
                int dot=key.LastIndexOf('.');
                if(dot<0 || key.Length-dot!=4 || !int.TryParse(key.Substring(dot+1),out _))continue;
                if(bodyMaterials.TryGetValue(key.Substring(0,dot),out var shared))materials[key]=shared;
            }
            // F23 keeps the accepted FPS anatomy and source GLB. Its costume surfaces
            // use the new world's canonical PBR palette, so both views share real
            // materials without duplicate slots or changing hand/weapon geometry.
            if(bodyMaterials.ContainsKey("vector-reference-coated-steel"))
            {
                var roles=new System.Collections.Generic.Dictionary<string,string>
                {
                    {"vector-fighter-identity-forearm-f9","vector-fighter-identity-reference"},
                    {"vector-assault-armor-forearm","vector-reference-coated-steel"},
                    {"material.001","vector-reference-woven-fabric"},
                    {"orbital-restored-vector-chassis","vector-reference-edge-metal"},
                    {"vector-ceramic-orbital","vector-fighter-identity-reference"},
                    {"orbital-seam-thread","vector-reference-webbing"},
                    {"vector-ceramic","vector-fighter-identity-reference"},
                    {"strata-oxblood-accent","vector-fighter-identity-reference"},
                    {"identity-vector-status","vector-fighter-identity-reference"}
                };
                foreach(var role in roles)if(materials.ContainsKey(role.Key))materials[role.Key]=bodyMaterials[role.Value];
            }
            var meshes=source.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Select(r=>r.sharedMesh).Where(m=>m).GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
            ground.TrooperBodyPrefab=Persist(source,"TrooperBody",ground.TrooperClips,materials,meshes);
            ground.TrooperArmsPrefab=Persist(arms,"TrooperArms",ground.TrooperClips,materials,meshes);
            // License travels with the Player, not just the repository.
            Directory.CreateDirectory("Assets/StreamingAssets");
            File.Copy(Art+"trooper-ATTRIBUTION.md","Assets/StreamingAssets/trooper-ATTRIBUTION.txt",true);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        static GameObject Persist(GameObject source,string name,AnimationClip[] clips,System.Collections.Generic.Dictionary<string,Material> materials,
            System.Collections.Generic.Dictionary<string,Mesh> meshes)
        {
            var instance=UnityEngine.Object.Instantiate(source); instance.name=name;
            try
            {
                var animator=instance.GetComponentInChildren<Animator>(true);
                if(!animator) throw new BuildFailedException("Missing imported Animator");
                animator.runtimeAnimatorController=null; animator.applyRootMotion=false;
                foreach(var clip in clips)
                    foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                        if(binding.path!="" && !animator.transform.Find(binding.path))
                            throw new BuildFailedException(name+" missing animation binding "+binding.path);
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>
                    {
                        if(!m || !materials.TryGetValue(m.name,out var shared)) throw new BuildFailedException("Unmapped trooper material: "+(m?m.name:"null"));
                        return shared;
                    }).ToArray();
                    if(renderer is SkinnedMeshRenderer skin)
                    {
                        var mesh=skin.sharedMesh;
                        if(mesh&&meshes.TryGetValue(mesh.name,out var sharedMesh)&&mesh!=sharedMesh)
                        {
                            // The shared forearms/equipment are exported in both GLBs. Reuse
                            // their body subasset only after checking vertex and skin identity.
                            bool same=mesh.vertices.SequenceEqual(sharedMesh.vertices)&&mesh.normals.SequenceEqual(sharedMesh.normals)&&
                                mesh.uv.SequenceEqual(sharedMesh.uv)&&mesh.bindposes.SequenceEqual(sharedMesh.bindposes)&&
                                mesh.boneWeights.SequenceEqual(sharedMesh.boneWeights)&&mesh.subMeshCount==sharedMesh.subMeshCount;
                            for(int sub=0;same&&sub<mesh.subMeshCount;sub++)
                                same=mesh.GetTopology(sub)==sharedMesh.GetTopology(sub)&&mesh.GetIndices(sub).SequenceEqual(sharedMesh.GetIndices(sub));
                            if(!same)throw new BuildFailedException("Same-name trooper meshes differ: "+mesh.name);
                            skin.sharedMesh=sharedMesh;
                        }
                        skin.updateWhenOffscreen=true;
                        // Conservative content bounds cover authored fall and extended arms, independent of gameplay.
                        skin.localBounds=new Bounds(Vector3.up*.9f,Vector3.one*4);
                    }
                }
                foreach(var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                return PrefabUtility.SaveAsPrefabAsset(instance,Output+name+".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }
}

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
            ground.TrooperBodyPrefab=Persist(source,"TrooperBody",ground.TrooperClips,materials);
            ground.TrooperArmsPrefab=Persist(arms,"TrooperArms",ground.TrooperClips,materials);
            // License travels with the Player, not just the repository.
            Directory.CreateDirectory("Assets/StreamingAssets");
            File.Copy(Art+"trooper-ATTRIBUTION.md","Assets/StreamingAssets/trooper-ATTRIBUTION.txt",true);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        static GameObject Persist(GameObject source,string name,AnimationClip[] clips,System.Collections.Generic.Dictionary<string,Material> materials)
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

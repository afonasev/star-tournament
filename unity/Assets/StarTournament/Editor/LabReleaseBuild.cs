using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StarTournament.ProvingGround.Editor
{
    public static class LabReleaseBuild
    {
        const string CatalogPath="Assets/StarTournament/Resources/LabReleaseCatalog.json";
        static LabBundle Baseline()=>UnityEngine.Object.FindAnyObjectByType<ProvingGround>().CaptureLabBundle();
        [MenuItem("Star Tournament/Lab/Bootstrap authoring project")]
        public static void BootstrapAuthoring()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string target=LabProjectStorage.ResolvePath(project);
            if(File.Exists(target)||Directory.Exists(target))throw new BuildFailedException("Author history already exists; never overwrite: "+target);
            const string scenePath="Assets/StarTournament/Scenes/ProvingGround.unity";
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);bool opened=!scene.isLoaded;
            if(!opened&&scene.isDirty)throw new BuildFailedException("Save and review the scene before authoring bootstrap");
            if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            try
            {
                var owner=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
                var baseline=owner.CaptureLabAuthoringBaseline();var seed=LabReleaseCatalog.Load();
                var history=LabProjectStorage.Open(project,baseline,seed);
                if(history.StorageError!=null||history.ReleaseError!=null)throw new BuildFailedException(history.StorageError??history.ReleaseError);
                if(history.Selected.Hash!=DesignLabHistory.ResolvePublishedDefault(baseline,seed).Selected.Hash)
                    throw new BuildFailedException("Author seed differs from the reviewed packaged Default");
                history.PersistAuthoring();
                var reopened=LabProjectStorage.Open(project,baseline,seed);
                if(reopened.StorageError!=null||reopened.ReleaseError!=null||reopened.Selected.Hash!=history.Selected.Hash)
                    throw new BuildFailedException("Author history readback failed; preserve the file for inspection");
                Debug.Log("STAR_LAB_AUTHORING_BOOTSTRAPPED "+target+" profiles="+history.Profiles.Count+" default="+history.Selected.Number+" hash="+history.Selected.Hash);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        [MenuItem("Star Tournament/Lab/Validate authoring project")]
        public static void ValidateAuthoring()
        {
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string path=LabProjectStorage.ResolvePath(project);
            if(!File.Exists(path))throw new BuildFailedException("Missing author history; bootstrap explicitly first");
            const string scenePath="Assets/StarTournament/Scenes/ProvingGround.unity";
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);bool opened=!scene.isLoaded;
            if(!opened&&scene.isDirty)throw new BuildFailedException("Save and review the scene before authoring validation");
            if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            try
            {
                var owner=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
                var baseline=owner.CaptureLabAuthoringBaseline();var seed=LabReleaseCatalog.Load();
                var history=LabProjectStorage.Open(project,baseline,seed);var projected=history.BuildReleaseCatalog();
                foreach(var entry in projected.Entries)
                {
                    var published=seed.Entries.FirstOrDefault(e=>e.ProfileId==entry.ProfileId&&e.Revision==entry.Revision);
                    if(published!=null&&published.Hash!=entry.Hash)throw new BuildFailedException("Published version identity changed: "+entry.ProfileId+" v"+entry.Revision);
                }
                var resolved=DesignLabHistory.ResolvePublishedDefault(baseline,projected);
                if(resolved.Selected.Hash!=history.Selected.Hash)throw new BuildFailedException("Projected Default differs from the author project");
                Debug.Log("STAR_LAB_AUTHORING_VALIDATED entries="+projected.Entries.Count+" defaultVersion="+resolved.Selected.ReleaseNumber+" effectiveHash="+resolved.Selected.Hash);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        public static void Bootstrap()
        {
            ProvingGroundBuild.Prepare();
            if(File.Exists(CatalogPath))throw new BuildFailedException("Catalog already exists; never overwrite release history");
            Write(LabReleaseCatalog.Factory(Baseline()));
            Validate();
        }
        public static void Validate()
        {
            if(!File.Exists(CatalogPath))throw new BuildFailedException("Missing LabReleaseCatalog; explicitly bootstrap before first release");
            var catalog=JsonUtility.FromJson<LabReleaseCatalog>(File.ReadAllText(CatalogPath));
            var history=new DesignLabHistory(Path.Combine(Path.GetTempPath(),"st-release-check-"+Guid.NewGuid()+".json"),Baseline(),releases:catalog,resetToLatestDefault:true);
            foreach(var entry in catalog.Entries)
                if(!history.Profiles.Single(p=>p.Id==entry.ProfileId).Revisions.Any(r=>r.ReleaseSequence==entry.Sequence&&history.Compatible(r)))
                    throw new BuildFailedException("Release is not runnable: "+entry.Sequence);
        }
        // Explicit source staging only; Player build/publication remain separate commands.
        [MenuItem("Star Tournament/Lab/Stage marked project versions")]
        public static void PromoteMarked()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new BuildFailedException("Остановите Play Mode перед подготовкой каталога");
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            string canonical=LabProjectStorage.ResolvePath(project);
            string requested=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_LAB_RELEASE_HISTORY");
            if(!string.IsNullOrEmpty(requested)&&(!Path.IsPathFullyQualified(requested)||Path.GetFullPath(requested)!=canonical))
                throw new BuildFailedException("Stage uses only the project author file: "+canonical);
            const string scenePath="Assets/StarTournament/Scenes/ProvingGround.unity";
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);bool opened=!scene.isLoaded;
            if(!opened&&scene.isDirty)throw new BuildFailedException("Save and review the scene before catalogue staging");
            if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            try
            {
                var owner=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
                bool changed=LabReleaseStaging.Stage(project,owner.CaptureLabAuthoringBaseline());
                if(changed)AssetDatabase.ImportAsset(CatalogPath,ImportAssetOptions.ForceUpdate);
                Debug.Log("STAR_LAB_RELEASES_STAGED project="+canonical+" changed="+changed+"; no build or publication");
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        static void Write(LabReleaseCatalog catalog)
        {
            string temp=CatalogPath+".tmp";
            try
            {
                File.WriteAllText(temp,JsonUtility.ToJson(catalog,true),new UTF8Encoding(false));
                if(File.Exists(CatalogPath))File.Replace(temp,CatalogPath,null);else File.Move(temp,CatalogPath);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
            AssetDatabase.ImportAsset(CatalogPath,ImportAssetOptions.ForceUpdate);
        }
    }
}

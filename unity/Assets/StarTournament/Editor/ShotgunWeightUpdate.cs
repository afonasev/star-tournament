using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace StarTournament.ProvingGround.Editor
{
    public static class ShotgunWeightUpdate
    {
        // Explicit one-time source authoring. Does not build, deploy or touch user Lab storage.
        public static void Stage()
        {
            var scene=EditorSceneManager.OpenScene("Assets/StarTournament/Scenes/ProvingGround.unity");
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            var baseline=ground.CaptureLabAuthoringBaseline();
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            var previous=LabReleaseCatalog.Load();
            var history=LabProjectStorage.Open(project,baseline,previous);
            if(history.StorageError!=null||history.ReleaseError!=null)throw new InvalidOperationException(history.StorageError??history.ReleaseError);
            var draft=history.Selected.Snapshot;
            // Publish a shotgun-only delta against actual gameplay, not old authoring
            // values that would freeze pre-migration visuals into the current schema.
            var gameplay=DesignLabHistory.ResolvePublishedDefault(baseline,previous).Selected.Snapshot;
            foreach(var descriptor in draft.Descriptors)
                if(draft.IsEditable(descriptor.Path))draft.Set(descriptor.Path,gameplay.Get(descriptor.Path));
            float before=draft.Get("shot.spread");
            if(!Mathf.Approximately(before,6)&&!Mathf.Approximately(before,4.8f))throw new InvalidOperationException("Unexpected shotgun baseline "+before);
            draft.Set("shot.spread",4.8f);
            draft.Set("presentation.shotTracerRed",.32f);draft.Set("presentation.shotTracerGreen",.32f);draft.Set("presentation.shotTracerBlue",.32f);
            if(draft.Hash()!=history.Selected.Hash||history.CanMaterializeSelected){history.Save(draft);history.MarkSelectedForRelease(true);}
            history.RefreshAuthoringCompatibility();
            LabReleaseStaging.Stage(project,baseline);
            AssetDatabase.Refresh();
            var updated=LabReleaseCatalog.Load();
            foreach(var old in previous.Entries)
            {
                var entry=updated.Entries.Single(e=>e.ProfileId==old.ProfileId&&e.Revision==old.Revision);
                if(JsonUtility.ToJson(entry)!=JsonUtility.ToJson(old))throw new InvalidOperationException("Historical release changed");
            }
            Debug.Log("SHOTGUN_WEIGHT_STAGED revision="+history.Selected.Number+" hash="+history.Selected.Hash+" spread="+draft.Get("shot.spread"));
        }
    }
}

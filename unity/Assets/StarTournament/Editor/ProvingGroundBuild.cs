using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StarTournament.ProvingGround.Editor
{
    public static class ProvingGroundBuild
    {
        const string Root = "Assets/StarTournament/";
        const string Scene = Root + "Scenes/ProvingGround.unity";
        [MenuItem("Star Tournament/Prepare proving ground")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Root+"Settings"); Directory.CreateDirectory(Root+"Scenes"); Directory.CreateDirectory(Root+"Resources");
            AssetDatabase.Refresh();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"Settings/ProvingURP.asset");
            if (!pipeline)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer,Root+"Settings/ProvingRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline,Root+"Settings/ProvingURP.asset");
            }
            GraphicsSettings.defaultRenderPipeline=pipeline; QualitySettings.renderPipeline=pipeline;
            if (!AssetDatabase.LoadAssetAtPath<Material>(Root+"Resources/ProvingLit.mat"))
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")),Root+"Resources/ProvingLit.mat");
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue=1; settings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.companyName="Afonasev"; PlayerSettings.productName="Star Tournament Unity Proving Ground";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground=false; PlayerSettings.defaultScreenWidth=1920; PlayerSettings.defaultScreenHeight=1080;
            PlayerSettings.enableFrameTimingStats=true;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Star Tournament Proving Ground").AddComponent<ProvingGround>();
            root.RobotPrefab=Required("robot"); root.WorldWeaponPrefab=Required("world-weapon"); root.ViewWeaponPrefab=Required("view-weapon");
            EditorSceneManager.SaveScene(scene,Scene);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};
            AssetDatabase.SaveAssets();
        }
        [System.Serializable] class Manifest { public Entry[] entries; }
        [System.Serializable] class Entry { public string key; public string path; }
        static GameObject Required(string name)
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"Art/asset-manifest.json"));
            var entry=manifest.entries.Single(e=>e.key==name);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(entry.path);
            if (!prefab) throw new BuildFailedException("GLB import missing: "+name);
            return prefab;
        }
        [MenuItem("Star Tournament/Build macOS proving ground")]
        public static void BuildMac()
        {
            Prepare();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{Scene}, locationPathName="Builds/StarTournamentProvingGround.app", target=BuildTarget.StandaloneOSX, options=BuildOptions.Development });
            if (report.summary.result!=BuildResult.Succeeded) throw new BuildFailedException(report.summary.result.ToString());
            Debug.Log("STAR_PROVING_BUILD_PASS " + report.summary.totalSize);
        }
    }

    [CustomEditor(typeof(ProvingGround))]
    public sealed class ProvingGroundInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var ground=(ProvingGround)target;
            EditorGUILayout.LabelField(ground.Profile.Id+" / v"+ground.Profile.Version,EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Профиль полигона. Диапазоны и validation используют один registry. Перезапустите Play после изменения.",MessageType.Info);
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            foreach(var descriptor in ground.Profile.Descriptors)
            {
                float value=ground.Profile.Get(descriptor.Path);
                float next=EditorGUILayout.Slider(new GUIContent(descriptor.Label+" ("+descriptor.Unit+")",descriptor.Description+"\n"+descriptor.Path),value,descriptor.Minimum,descriptor.Maximum);
                next=descriptor.Minimum+Mathf.Round((next-descriptor.Minimum)/descriptor.Step)*descriptor.Step;
                if (!Mathf.Approximately(value,next)) { Undo.RecordObject(ground,"Change proving profile");ground.Profile.Set(descriptor.Path,Mathf.Clamp(next,descriptor.Minimum,descriptor.Maximum));EditorUtility.SetDirty(ground); }
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}

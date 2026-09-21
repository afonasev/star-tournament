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
        public static void Prepare() => PrepareFamily(0,1920,1080);
        static void PrepareFamily(int arenaFamily,int width,int height)
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
            PlayerSettings.runInBackground=false; PlayerSettings.defaultScreenWidth=width; PlayerSettings.defaultScreenHeight=height;
            PlayerSettings.enableFrameTimingStats=true;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Star Tournament Proving Ground").AddComponent<ProvingGround>();
            root.ArenaProfile.Set("arena.family",arenaFamily);
            TrooperPreparation.Prepare(root,Required("player-body"),Required("player-hands"));
            root.SpeedPickupPrefab=Required("speed-pickup");
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
            var familyText=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_ARENA_FAMILY");
            int family=0;
            if(!string.IsNullOrEmpty(familyText)&&(!int.TryParse(familyText,out family)||family<0||family>=ArenaFamilyCatalog.Ids.Count))
                throw new BuildFailedException("Invalid STAR_TOURNAMENT_QA_ARENA_FAMILY");
            int width=ReadQaDimension("STAR_TOURNAMENT_QA_WIDTH",1920),height=ReadQaDimension("STAR_TOURNAMENT_QA_HEIGHT",1080);
            PrepareFamily(family,width,height);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{Scene}, locationPathName="Builds/StarTournamentProvingGround.app", target=BuildTarget.StandaloneOSX, options=BuildOptions.Development,
                extraScriptingDefines=new[]{"STAR_TOURNAMENT_DEVELOPMENT_QA"} });
            // Keep the editable canonical scene at its default family after a one-off QA artifact build.
            Prepare();
            if (report.summary.result!=BuildResult.Succeeded) throw new BuildFailedException(report.summary.result.ToString());
            Debug.Log("STAR_PROVING_BUILD_PASS " + report.summary.totalSize);
        }
        static int ReadQaDimension(string variable,int fallback)
        {
            var value=System.Environment.GetEnvironmentVariable(variable);
            if(string.IsNullOrEmpty(value))return fallback;
            if(!int.TryParse(value,out int dimension)||dimension<320||dimension>7680)throw new BuildFailedException("Invalid "+variable);
            return dimension;
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
            foreach(var profile in new[]{ground.Profile,ground.ArenaProfile,ground.LifeProfile,ground.CombatProfile,ground.MatchProfile,ground.TeamProfile,ground.TrooperProfile,ground.BotPerceptionProfile,ground.BotNavigationProfile,ground.RosterProfile,ground.BotBehaviorProfile})
            {
            EditorGUILayout.LabelField(profile.Id+" / v"+profile.Version,EditorStyles.boldLabel);
            foreach(var descriptor in profile.Descriptors)
            {
                float value=profile.Get(descriptor.Path);
                float next=EditorGUILayout.Slider(new GUIContent(descriptor.Label+" ("+descriptor.Unit+")",descriptor.Description+"\n"+descriptor.Path),value,descriptor.Minimum,descriptor.Maximum);
                next=descriptor.Minimum+Mathf.Round((next-descriptor.Minimum)/descriptor.Step)*descriptor.Step;
                if (!Mathf.Approximately(value,next)) { Undo.RecordObject(ground,"Change proving profile");profile.Set(descriptor.Path,Mathf.Clamp(next,descriptor.Minimum,descriptor.Maximum));EditorUtility.SetDirty(ground); }
            }
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}

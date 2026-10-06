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
        public static void Prepare() => PrepareAuthored(1920,1080);
        static void PrepareAuthored(int width,int height)
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
            // Runtime-created panoramic materials need an explicit build-time shader dependency.
            if (!AssetDatabase.LoadAssetAtPath<Material>(Root+"Resources/ProvingUnlit.mat"))
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Unlit")),Root+"Resources/ProvingUnlit.mat");
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue=1; settings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.companyName="Afonasev"; PlayerSettings.productName="Star Tournament Unity Proving Ground";
            var appIcon=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Branding/AppIcon.png");
            if(!appIcon)throw new BuildFailedException("Approved app icon is missing");
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone,
                PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone,IconKind.Any).Select(_=>appIcon).ToArray(),IconKind.Any);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.runInBackground=false; PlayerSettings.defaultScreenWidth=width; PlayerSettings.defaultScreenHeight=height;
            PlayerSettings.enableFrameTimingStats=true;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Star Tournament Proving Ground").AddComponent<ProvingGround>();
            root.CombatBowlReviewDirectory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_COMBAT_BOWL_EVIDENCE");
            TrooperPreparation.Prepare(root,Required("player-body"),Required("player-hands"));
            root.RiflePrefab=Required("rifle-lod0");
            root.PulsePrefab=Required("pulse-lod0");
            root.RocketProjectilePrefab=Required("pulse-projectile");
            root.CutterPrefab=Required("cutter-lod0");
            root.ArmorPickupPrefab=Required("armor-pickup");
            root.SpeedPickupPrefab=Required("speed-pickup");
            root.DamagePickupPrefab=Required("damage-pickup");
            root.HealPickupPrefab=Required("heal-pickup");
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
            if(!string.IsNullOrEmpty(familyText))throw new BuildFailedException("Procedural QA source is unsupported.");
            int width=ReadQaDimension("STAR_TOURNAMENT_QA_WIDTH",1920),height=ReadQaDimension("STAR_TOURNAMENT_QA_HEIGHT",1080);
            PrepareAuthored(width,height);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{Scene}, locationPathName="Builds/StarTournamentProvingGround.app", target=BuildTarget.StandaloneOSX, options=BuildOptions.Development,
                extraScriptingDefines=new[]{"STAR_TOURNAMENT_DEVELOPMENT_QA"} });
            // Keep the editable canonical scene at its authored catalog after a one-off QA artifact build.
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
        public static void BuildReleaseMac() => BuildRelease(BuildTarget.StandaloneOSX);
        public static void BuildReleaseWindows() => BuildRelease(BuildTarget.StandaloneWindows64);
        static void BuildRelease(BuildTarget target)
        {
            var output=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RELEASE_OUTPUT");
            if(string.IsNullOrWhiteSpace(output)||!Path.IsPathFullyQualified(output))
                throw new BuildFailedException("Absolute STAR_TOURNAMENT_RELEASE_OUTPUT required");
            Prepare();
            try
            {
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=new[]{Scene},
                    locationPathName=output, target=target, options=BuildOptions.None });
                if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException(report.summary.result.ToString());
                Debug.Log("STAR_RELEASE_BUILD_PASS "+target+" "+report.summary.totalSize);
            }
            finally { Prepare(); }
        }
    }

    [CustomEditor(typeof(ProvingGround))]
    public sealed class ProvingGroundInspector : UnityEditor.Editor
    {
        bool showBowl;
        public override void OnInspectorGUI()
        {
            var ground=(ProvingGround)target;
            EditorGUILayout.LabelField(ground.Profile.Id+" / v"+ground.Profile.Version,EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Профиль полигона. Диапазоны и validation используют один registry. Перезапустите Play после изменения.",MessageType.Info);
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            showBowl=EditorGUILayout.Foldout(showBowl,"Combat Bowl authoring metadata (published revision remains frozen)");
            foreach(var profile in new[]{ground.Profile,ground.OrbitalLeagueProfile,ground.LifeProfile,ground.CombatProfile,ground.MatchProfile,ground.TeamProfile,ground.TrooperProfile,ground.DeathProfile,ground.BotPerceptionProfile,ground.BotNavigationProfile,ground.RosterProfile,ground.BotBehaviorProfile,ground.CutterProfile,ground.RocketEffectsProfile}.Concat(showBowl?new[]{ground.CombatBowlAuthoring}:System.Array.Empty<ProvingProfile>()))
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        DesignLabHistory labHistory; LabBundle labDraft,labBaseline;
        ScrollRect labTreeScroll,labFieldsScroll; Transform labTree,labFields;
        Text labIdentity,labStatus,labHint,labFieldsTitle,labReleaseReason,labDefaultSource; Button labSave,labDelete,labBack,labProfileSelector,labRevisionSelector,labRelease,labCreate,labRename,labResetRelease;
        InputField labSearch; Button labCancelAction; GameObject labDialog; string labGroup="rifle",labQuery="",labError;
        GameObject labDialogReturn; readonly Dictionary<Selectable,bool> labDialogControls=new Dictionary<Selectable,bool>();
        bool labAllChanges; readonly Dictionary<string,string> labRaw=new Dictionary<string,string>();
        readonly Dictionary<string,LabFieldView> labViews=new Dictionary<string,LabFieldView>();
        sealed class LabFieldView {public InputField Input;public Selectable Focus;public Button Switch;public Text Diff,Error;public Image Background;}
        public string FrozenLabIdentity {get;private set;}
        public string FrozenLabPresentation {get;private set;}
        LabRevisionReference frozenLabReference;
        string labSavedIdentity="",labSavedPresentation="",labSavedRevisionLabel="",labActiveLabel="",labWriteMessage;
        LabBundle labParameterDefaults;string labParameterDefaultsError;
        NumericDescriptor[] labRegistry=Array.Empty<NumericDescriptor>();
        public string LabSavedIdentity=>labSavedIdentity;
        public bool LabSelectionPending=>labHistory!=null&&labHistory.Busy;
        void CacheLabIdentity(DesignLabHistory selection=null)
        {
            selection=selection??labHistory;
            var revision=selection.Selected;
            var saved=selection.SavedIdentity(selection.SelectedProfileId,revision.Number);
            activeLabSnapshot=revision.Snapshot.Clone();
            activeLabReference=new LabRevisionReference{ProfileId=selection.SelectedProfileId,Revision=saved.Number,Hash=activeLabSnapshot.Hash()};
            labSavedRevisionLabel=LabRevisionLabel(selection,revision);
            labActiveLabel=LabCompactName(selection.SelectedProfileName)+" · "+labSavedRevisionLabel;
            labSavedIdentity=selection.SelectedProfileName+" · "+labSavedRevisionLabel+"\n"+selection.SelectedProfileId+"\nSHA256 "+saved.Hash;
            string date=string.IsNullOrEmpty(saved.Date)?"без даты":saved.Date;
            if(DateTime.TryParseExact(saved.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var knownDate))date=knownDate.ToString("dd.MM.yyyy",CultureInfo.InvariantCulture);
            labSavedPresentation="Баланс матча: "+selection.SelectedProfileName+"\nВерсия "+saved.Number+" · "+date;
        }
        bool LabDirty=>labDraft!=null&&(labRaw.Count>0||labDraft.Hash()!=labBaseline.Hash());
        LabBundle CurrentLabBundle()=>new LabBundle{Profiles=new List<ProvingProfile>{Profile,LifeProfile,CombatProfile,TrooperProfile,MatchProfile,TeamProfile,RosterProfile,BotPerceptionProfile,BotNavigationProfile,BotBehaviorProfile,OrbitalLeagueProfile,CombatBowlAuthoring,CutterProfile,PlasmaProfile,ParticipantPaletteProfile,ProvingProfile.CreateBotEvaluationDefault(),DeathProfile,BloodProfile,RocketEffectsProfile,TunnelsPresentation,TunnelsAuthoring,LunarPresentation,LunarAuthoring,WellProfile,WellAuthoring,HitFeedbackProfile,LobbyProfile}};
        public LabBundle CaptureLabBundle()=>CurrentLabBundle().Clone();
        void PrepareLabRegistry()
        {
            MatchProfile.EnsureMatchDescriptors();TrooperProfile.EnsureTrooperDescriptors();PlasmaProfile.EnsurePlasmaDescriptors();
            ParticipantPaletteProfile.EnsureIdentitySurfaceDescriptors();RocketEffectsProfile.EnsureRocketEffectsDescriptors();
            Profile.EnsureDefaultDescriptors();LifeProfile.EnsureCombatDescriptors();CombatProfile.EnsureNativeCombatDescriptors();
            OrbitalLeagueProfile.EnsureOrbitalLeagueDescriptors();BotBehaviorProfile.EnsureBotDescriptors();BotPerceptionProfile.EnsureBotDescriptors();
        }
        public LabBundle CaptureLabAuthoringBaseline(){PrepareLabRegistry();return CurrentLabBundle().Clone();}
        static string LabRevisionLabel(DesignLabHistory history,LabRevision revision,string profileId=null)=>history.SavedIdentity(profileId??history.SelectedProfileId,revision.Number).Label;
        CancellationTokenSource historyCancellation;
        Task<DesignLabHistory> historyLoading;
        LabBundle activeLabSnapshot,historyStartupBaseline;
        LabReleaseCatalog historyStartupCatalogue;
        LabRevisionReference activeLabReference;
        string historyStartupPath,historyStartupLegacyPath,historyLoadError,labProjectRoot;
        bool authorStartupResolved,authorSessionFallback;
        GameObject authorHistoryFailure;Text authorHistoryFailureReason,authorHistoryFailureSession;Button authorHistoryBack,authorHistoryDefault;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static Func<string,LabBundle,LabReleaseCatalog,CancellationToken,DesignLabHistory> LabHistoryLoaderForTests;
#endif
        public bool IsLabHistoryReady=>labHistory!=null&&historyLoadError==null;
        public bool IsLabHistoryLoading=>historyLoading!=null;
        public string LabHistoryLoadError=>historyLoadError;
        public LabRevisionReference ActiveLabReference=>activeLabReference?.Clone();
        public LabHistoryStartupDiagnostics LabHistoryDiagnostics=>labHistory?.StartupDiagnostics;
        void OnApplicationQuit()=>CancelHistoryLoading(true);
        void CancelHistoryLoading(bool waitForCompletion=false)
        {
            var cancellation=historyCancellation;if(cancellation==null)return;
            historyCancellation=null;cancellation.Cancel();
            var pending=historyLoading;historyLoading=null;
            if(pending==null){cancellation.Dispose();return;}
            if(waitForCompletion)
            {
                // JsonUtility is allowed on this worker, but Unity's serializer must
                // remain alive until its cancelled work has actually stopped.
                try{pending.GetAwaiter().GetResult();}
                catch(OperationCanceledException){}
                catch(Exception error){Debug.LogWarning("Lab shutdown: "+error.Message);}
                finally{cancellation.Dispose();}
                return;
            }
            pending.ContinueWith(done=>{var observed=done.Exception;cancellation.Dispose();},TaskScheduler.Default);
        }
        IEnumerator InitializeDesignLabAsync()
        {
            // Registry v2 adds heal fields and replaces the authored basement armor paths.
            // Keep v1 histories and their immutable hashes intact; v2 starts from the new shipped baseline.
            string historyPath=Path.Combine(Application.persistentDataPath,VersionedUserData.LabHistory);
            bool useLegacyHistory=true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool editor=false;
#if UNITY_EDITOR
            editor=true;
#endif
            labProjectRoot=LabProjectStorage.SelectProject(editor,true,Path.GetFullPath(Path.Combine(Application.dataPath,"..")),
                Environment.GetCommandLineArgs(),Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY"),Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_PROJECT"));
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY")))
            {historyPath=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");useLegacyHistory=false;}
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-labReview");
            if(flag>=0&&flag+1<args.Length){historyPath=Path.Combine(args[flag+1],"history.json");useLegacyHistory=false;}
            int colors=Array.IndexOf(args,"-colorIdentityEvidence");
            if(Array.IndexOf(args,"-colorIdentityReview")>=0&&colors>=0&&colors+1<args.Length)
            {historyPath=Path.Combine(args[colors+1],"qa-lab-history.json");useLegacyHistory=false;}
#endif
            if(labProjectRoot!=null){historyPath=LabProjectStorage.ResolvePath(labProjectRoot);useLegacyHistory=false;}
            var startupWatch=System.Diagnostics.Stopwatch.StartNew();
            historyStartupPath=historyPath;
            historyStartupLegacyPath=useLegacyHistory
                ?Path.Combine(Application.persistentDataPath,VersionedUserData.LegacyLabHistory):null;
            // Capture trusted baseline before published values change live profile owners.
            historyStartupBaseline=CurrentLabBundle().Clone();
            historyStartupCatalogue=LabReleaseCatalog.Load();
            var published=DesignLabHistory.ResolvePublishedDefault(historyStartupBaseline,historyStartupCatalogue);
            CacheLabIdentity(published);labBaseline=activeLabSnapshot.Clone();labDraft=labBaseline.Clone();
            ApplySavedLabRevision();
            if(labProjectRoot!=null)yield return LoadDesignLabHistory(true);
            Application.wantsToQuit+=ProtectLabQuit;
            Debug.Log("Lab startup: initial configuration resolved in "+startupWatch.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture)+" seconds");
            yield return null;
        }
        IEnumerator LoadDesignLabHistoryInBackground()=>LoadDesignLabHistory(false);
        IEnumerator LoadDesignLabHistory(bool authorStartup)
        {
            // Production defers history IO until the menu. Author startup yields frames
            // under the loading screen while the project's Default is validated.
            yield return null;
            if((!authorStartup&&(!IsReady||labProjectRoot!=null))||historyLoading!=null||labHistory!=null)yield break;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            var cancellation=new CancellationTokenSource();historyCancellation=cancellation;
            var token=cancellation.Token;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var overrideLoader=LabHistoryLoaderForTests;
#else
            Func<string,LabBundle,LabReleaseCatalog,CancellationToken,DesignLabHistory> overrideLoader=null;
#endif
            var pending=Task.Run(()=>overrideLoader!=null?overrideLoader(historyStartupPath,historyStartupBaseline,historyStartupCatalogue,token):new DesignLabHistory(historyStartupPath,historyStartupBaseline,releases:historyStartupCatalogue,resetToLatestDefault:true,persistMigration:false,cancellationToken:token,legacyPath:historyStartupLegacyPath,authoring:labProjectRoot!=null),token);
            historyLoading=pending;
            Debug.Log("Lab startup: validating local history in background");
            while(!pending.IsCompleted)yield return null;
            if(historyLoading!=pending||token.IsCancellationRequested)yield break;
            try
            {
                var loaded=pending.GetAwaiter().GetResult();
                if(loaded.StorageError!=null)historyLoadError=loaded.StorageError;
                else if(labProjectRoot!=null)
                {
                    labHistory=loaded;
                    if(loaded.ReleaseError==null){CacheLabIdentity(loaded);ApplySavedLabRevision();authorStartupResolved=true;}
                }
                else if(loaded.Selected.Snapshot.Hash()!=activeLabReference.Hash)
                    historyLoadError="История Lab не соответствует опубликованному Default. Исходный файл сохранён.";
                else labHistory=loaded;
                Debug.Log("Lab startup: local history complete in "+watch.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture)+" seconds; "+loaded.StartupDiagnostics);
            }
            catch(OperationCanceledException) { }
            catch(Exception e){historyLoadError="Не удалось открыть историю Lab. Исходный файл сохранён: "+e.Message;Debug.LogException(e);}
            finally
            {
                if(historyLoading==pending){historyLoading=null;historyCancellation=null;cancellation.Dispose();}
            }
            // Production background loading never applies a match/draft. Author startup
            // applies project Default before the menu or any match can become ready.
            if(phase==Phase.Lab)
            {
                SetLabHistoryWaiting(false);
                if(IsLabHistoryReady){ResetLabDraft();RefreshLabWorkspace();}
                else SetLabHistoryWaiting(true);
            }
        }
        bool ProtectLabQuit()
        {
            if(labHistory!=null&&labHistory.Busy)return false;
            if(phase!=Phase.Lab||!LabDirty)return true;
            ConfirmLabDiscard(()=>Application.Quit());return false;
        }
        void ApplySavedLabRevision()
        {
            if(activeLabSnapshot==null)return;var saved=activeLabSnapshot;
            // Runtime profile objects retain their identity for fixture/authoring references.
            // Match creation copies their values into separate frozen owners.
            foreach(var profile in CurrentLabBundle().Profiles)
                foreach(var descriptor in profile.Descriptors)profile.Set(descriptor.Path,saved.Get(descriptor.Path));
        }
        // Translate presentation only; historical descriptor bytes and Lab hashes remain unchanged.
        static string LabText(string text)=>text.Replace("Салага","Рекрут");
        static string GroupLabel(string group)
        {
            if(group=="visual-polish")return "Графика → Свет и эффекты";
            switch(group)
            {
                case "well-lifts":return "Колодец → Лифты";case "well-bots":return "Колодец → Боты";case "well-light":return "Колодец → Свет";case "well-art":return "Колодец → Оформление";
                case "tunnel-light":return "Тоннели → Свет";case "tunnel-surface":return "Тоннели → Поверхности";
                case "map-geometry":return "Карта → Геометрия (read-only)";case "map-spawn":return "Карта → Спавны (read-only)";case "map-transitions":return "Карта → Переходы (read-only)";case "map-routes":return "Карта → Маршруты (read-only)";case "map-bonuses":return "Карта → Бонусы (read-only)";
                case "hit-reaction":return "Оформление → Реакция на попадание";case "hit-body-blood":return "Оформление → Кровь на бойце";case "hit-shield":return "Оформление → Вспышки щита";
                case "plasma-grip":return "Оружие → FORGE · хват";case "plasma":return "Оружие → FORGE";case "plasma-effects":return "Оружие → FORGE · оформление";case "cutter-effects":return "Оружие → Резак · оформление";case "cutter-bots":return "Боты → Резак";case "cutter":return "Оружие → Резак";case "rocket-effects":return "Оружие → Pulse · оформление";case "rocket":return "Оружие → Pulse";case "rifle":return "Оружие → Винтовка";case "shotgun":return "Оружие → Дробовик";case "weapon-switch":return "Оружие → Переключение";
                case "weapon-pickup":return "Бонусы → Оружие";case "full-heal":return "Бонусы → Полное исцеление";case "armor-pickup":return "Бонусы → Броня";case "damage-boost":return "Бонусы → Урон";case "speed-pickup":return "Бонусы → Скорость";
                case "pickups":return "Бонусы → Подбор";case "combat":return "Игрок → Здоровье";case "motor":return "Игрок → Движение";
                case "capsule":return "Игрок → Капсула";case "camera":return "Камера → Вид игрока";case "input":return "Игрок → Управление";
                case "death":return "Смерть → Тело";case "spawn":return "Смерть → Возрождение";case "killcam":return "Камера → Killcam";
                case "bot-banter":return "Боты → Реплики";
                case "bot-evaluation":return "Боты → Контрольная оценка (read-only)";case "bots":return "Боты → Восприятие";case "bot-navigation":return "Боты → Навигация";case "bot-behavior":return "Боты → Поведение";
                case "lobby":return "Матч → Лобби";case "match":return "Матч → Правила";case "score":return "Матч → Очки";case "teams":return "Матч → Команды";
                case "damage-policy":return "Оружие → Урон союзникам и себе";case "hit-zones":return "Игрок → Зоны попадания";case "presentation":return "Оформление → Сцена";case "shot-feedback":return "Оформление → Выстрел";case "audio":return "Оформление → Звук";case "music":return "Оформление → Музыка";
                case "vector-shot-feedback":return "Оформление → Vector";case "simulation":return "Матч → Симуляция";
                default:return GroupDisplayNames.TryGetValue(group,out var label)?label:"Оформление → "+group;
            }
        }
        static readonly Dictionary<string,string> GroupDisplayNames=new Dictionary<string,string>{
            {"surfaces","Оформление → Поверхности"},{"details","Оформление → Детали"},{"lighting","Оформление → Свет"},{"broadcast","Оформление → Экраны"},{"palette","Оформление → Палитра"},{"atmosphere","Оформление → Атмосфера"},{"player-capsule","Игрок → Капсула"},{"player-movement","Игрок → Движение"},{"navigation","Арена → Навигация"},
            {"blood-drops","Оформление → Брызги крови"},{"blood-marks","Оформление → Следы крови"},{"blood-color","Оформление → Цвет крови"},{"death-impulse","Оформление → Отбрасывание тела"},{"death-ragdoll","Оформление → Физика тела"},{"scoring","Матч → Очки"},{"trooper-animation","Оформление → Анимация"},{"trooper-grip","Оформление → Хват оружия"},
            {"participant-colors","Оформление → Цвета участников"},{"trooper-view","Оформление → Модель игрока"},{"standings","Интерфейс → Таблица результатов"},{"damage-vignette","Оформление → Получение урона"},{"ui","Оформление → HUD"},{"world-query","Арена → Проверки мира"},
            {"ring","Оформление → Станция"},{"ring-layout","Оформление → Детали станции"},{"space","Оформление → Космос"},
            {"windows","Оформление → Иллюминаторы"},{"geometry","Карта → Геометрия"},{"transitions","Карта → Переходы"},
            {"routes","Карта → Маршруты"},{"bonuses","Карта → Точки бонусов"},{"weapon","Оружие → Диагностика"}};
        static void SelectLabControl(Selectable control){if(EventSystem.current)EventSystem.current.SetSelectedGameObject(control.gameObject);}
        void Hint(GameObject go,string text)
        {var hint=go.AddComponent<LabFocusHint>();hint.Show=()=>labHint.text=text;hint.Focus=()=>RevealLabFocus(go);}
        void RevealLabFocus(GameObject go)
        {
            foreach(var scroll in go.GetComponentsInParent<ScrollRect>())
            {
                if(scroll==null||!go.transform.IsChildOf(scroll.content))continue;
                Canvas.ForceUpdateCanvases();
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,(RectTransform)go.transform);
                float overflow=bounds.max.y>scroll.viewport.rect.yMax?bounds.max.y-scroll.viewport.rect.yMax:
                    bounds.min.y<scroll.viewport.rect.yMin?bounds.min.y-scroll.viewport.rect.yMin:0;
                float available=scroll.content.rect.height-scroll.viewport.rect.height;
                if(available>0&&overflow!=0)scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+overflow/available);
            }
        }
        // Editor layout uses Full HD design pixels; these are UI invariants, not gameplay tuning values.
        const float LabTreeRowHeight=34,LabFieldRowHeight=112,LabOptionRowHeight=38;
        Button LabButton(Transform parent,string name,string text,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var button=MenuButton(parent,name,text,min,max,action);
            button.GetComponentInChildren<Text>().fontSize=16;return button;
        }
        Button LabCancelButton(Transform parent,string name,string value,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
        {
            var button=LabButton(parent,name,value,min,max,action);labCancelAction=button;
            BindMenuShortcut(button,MenuPadButton.East,()=>phase==Phase.Lab&&labDialog&&button.transform.IsChildOf(labDialog.transform));return button;
        }
        InputField LabInput(Transform parent,string name,string text,Vector2 min,Vector2 max)
        {
            var panel=Panel(parent,name,min,max,new Color32(35,51,68,255));var input=panel.AddComponent<InputField>();
            input.textComponent=Label(panel.transform,"value",text,16,new Vector2(.04f,0),new Vector2(.96f,1),TextAnchor.MiddleLeft,Color.white);
            input.text=text;BindTextInput(input,name.StartsWith("input-",StringComparison.Ordinal));return input;
        }
        ScrollRect LabScroll(Transform parent,string name,Vector2 min,Vector2 max,out Transform content)
        {
            var panel=Panel(parent,name,min,max,MenuInk);var scroll=panel.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var viewport=Panel(panel.transform,"viewport",Vector2.zero,Vector2.one,MenuInk);viewport.AddComponent<RectMask2D>();
            var body=new GameObject("content",typeof(RectTransform));body.transform.SetParent(viewport.transform,false);
            var rect=(RectTransform)body.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=Vector2.zero;
            scroll.viewport=(RectTransform)viewport.transform;scroll.content=rect;content=body.transform;return scroll;
        }
        void CreateDesignLabUi(Transform parent)
        {
            labScreen=Panel(parent,"lab-screen",Vector2.zero,Vector2.one,MenuInk);
            labBack=LabButton(labScreen.transform,"lab-back","‹ НАЗАД",new Vector2(.04f,.845f),new Vector2(.15f,.90f),ToMainMenu);
            BindMenuShortcut(labBack,MenuPadButton.East,()=>phase==Phase.Lab&&labDialog==null&&(!authorHistoryFailure||!authorHistoryFailure.activeSelf));
            Label(labScreen.transform,"lab-heading","ЛАБОРАТОРИЯ ГЕЙМДИЗАЙНА",21,new Vector2(.17f,.845f),new Vector2(.70f,.90f),TextAnchor.MiddleLeft,MenuGold);
            var clear=LabButton(labScreen.transform,"lab-clear","Сбросить правки",new Vector2(.806f,.115f),new Vector2(.96f,.169f),()=>GuardLabSelection(()=>{ResetLabDraft();RefreshLabWorkspace();}));
            labSave=LabButton(labScreen.transform,"lab-save","Сохранить",new Vector2(.379f,.715f),new Vector2(.504f,.772f),()=>LabOperation(()=>labHistory.Save(labDraft),true));
            labSave.GetComponent<Image>().color=MenuGold;labSave.GetComponentInChildren<Text>().color=MenuInk;
            labResetRelease=LabButton(labScreen.transform,"lab-reset-release","Версия по умолчанию",new Vector2(.04f,.078f),new Vector2(.33f,.123f),()=>GuardLabSelection(()=>LabOperation(labHistory.ResetToRelease,true)));
            labDefaultSource=Label(labScreen.transform,"lab-default-source","",14,new Vector2(.35f,.078f),new Vector2(.65f,.123f),TextAnchor.MiddleLeft,Color.white);
            labHint=Label(labScreen.transform,"lab-hint","",14,new Vector2(.04f,.014f),new Vector2(.96f,.071f),TextAnchor.MiddleLeft,MenuGold);
            labHint.supportRichText=false;
            Hint(labResetRelease.gameObject,"Выбрать последнюю отмеченную версию Default из проекта или пакета. История сохраняется.");
            Hint(clear.gameObject,"Сбросить правки: вернуть все поля к выбранной базе; история сохраняется.");Hint(labSave.gameObject,"Сохранить: добавить новую неизменяемую ревизию. Можно сохранить правки или полную совместимую версию без ошибок.");
            labProfileSelector=LabButton(labScreen.transform,"lab-profile-select","Профиль ▾",new Vector2(.04f,.715f),new Vector2(.233f,.772f),ShowLabProfiles);
            labRevisionSelector=LabButton(labScreen.transform,"lab-revision-select","Ревизия ▾",new Vector2(.243f,.715f),new Vector2(.368f,.772f),ShowLabRevisions);
            labCreate=LabButton(labScreen.transform,"lab-create","Создать копию",new Vector2(.515f,.715f),new Vector2(.653f,.772f),()=>LabNameDialog(false));
            labRename=LabButton(labScreen.transform,"lab-rename","Переименовать",new Vector2(.664f,.715f),new Vector2(.782f,.772f),()=>LabNameDialog(true));
            labDelete=LabButton(labScreen.transform,"lab-delete","Удалить профиль",new Vector2(.793f,.715f),new Vector2(.96f,.772f),()=>GuardLabSelection(()=>LabConfirm("Удалить «"+labHistory.SelectedProfileName+"»?\nРевизий: "+labHistory.SelectedProfile.Revisions.Count,"Удалить профиль",()=>LabOperation(labHistory.DeleteSelected,true))));
            Hint(labDelete.gameObject,"Удаляется целый локальный профиль после подтверждения. Профили со встроенными релизами защищены от удаления.");
            LabButton(labScreen.transform,"lab-changes","Все изменения",new Vector2(.663f,.115f),new Vector2(.795f,.169f),()=>{labAllChanges=true;labQuery="";labSearch.SetTextWithoutNotify("");RefreshLabWorkspace();});
            labIdentity=Label(labScreen.transform,"lab-identity","",16,new Vector2(.04f,.13f),new Vector2(.65f,.195f),TextAnchor.MiddleLeft,Color.white);
            labRelease=LabButton(labScreen.transform,"lab-release","В релиз",new Vector2(.77f,.845f),new Vector2(.96f,.90f),()=>LabOperation(()=>labHistory.MarkSelectedForRelease(!labHistory.SelectedReleaseMarked),false,false));
            labRelease.GetComponentInChildren<Text>().fontSize=16;
            labReleaseReason=Label(labScreen.transform,"lab-release-reason","",14,new Vector2(.72f,.785f),new Vector2(.96f,.84f),TextAnchor.MiddleLeft,MenuGold);
            Hint(labRelease.gameObject,"Отметить сохранённую ревизию для будущего выпуска. Отметка локальная; выпуск выполняется отдельно. Правки сначала сохраните.");
            labSearch=LabInput(labScreen.transform,"lab-search","",new Vector2(.04f,.631f),new Vector2(.96f,.693f));
            labSearch.placeholder=Label(labSearch.transform,"search-placeholder","Поиск по названию, описанию или пути…",16,new Vector2(.04f,0),new Vector2(.96f,1),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            Hint(labSearch.gameObject,"Поиск во всех сущностях: название, влияние, группа или stable path. Очистите для возврата к выбранной сущности.");
            labSearch.onValueChanged.AddListener(q=>{labQuery=q;labAllChanges=false;RefreshLabWorkspace();});
            labStatus=Label(labScreen.transform,"lab-status","",14,new Vector2(.04f,.785f),new Vector2(.58f,.83f),TextAnchor.MiddleLeft,MenuGold);
            LabButton(labScreen.transform,"lab-errors","Ошибки",new Vector2(.60f,.785f),new Vector2(.70f,.83f),ShowLabErrors);
            labTreeScroll=LabScroll(labScreen.transform,"lab-tree",new Vector2(.04f,.20f),new Vector2(.236f,.607f),out labTree);
            labFieldsTitle=Label(labScreen.transform,"lab-fields-title","",18,new Vector2(.25f,.553f),new Vector2(.96f,.607f),TextAnchor.MiddleLeft,MenuGold);
            labFieldsScroll=LabScroll(labScreen.transform,"lab-fields",new Vector2(.25f,.20f),new Vector2(.96f,.548f),out labFields);
            BuildAuthorHistoryFailure();
        }
        void BuildAuthorHistoryFailure()
        {
            authorHistoryFailure=Panel(labScreen.transform,"author-history-failure",Vector2.zero,Vector2.one,MenuInk);
            Label(authorHistoryFailure.transform,"author-history-title","ИСТОРИЯ ПРОЕКТА НЕДОСТУПНА",40,new Vector2(.04f,.83f),new Vector2(.96f,.92f),TextAnchor.MiddleLeft,MenuGold);
            Label(authorHistoryFailure.transform,"author-history-subtitle","Исходный файл сохранён; запись в Lab заблокирована",26,new Vector2(.04f,.76f),new Vector2(.96f,.82f),TextAnchor.MiddleLeft,Color.white);
            var card=Panel(authorHistoryFailure.transform,"author-history-card",new Vector2(.04f,.15f),new Vector2(.96f,.73f),MenuCard);
            Label(card.transform,"author-history-heading","Не удалось открыть авторский баланс",34,new Vector2(.025f,.84f),new Vector2(.975f,.96f),TextAnchor.MiddleLeft,MenuGold);
            var scroll=LabScroll(card.transform,"author-history-details",new Vector2(.025f,.35f),new Vector2(.975f,.83f),out var content);
            authorHistoryFailureReason=Label(content,"author-history-reason","",30,Vector2.zero,Vector2.one,TextAnchor.UpperLeft,Color.white);
            authorHistoryFailureReason.supportRichText=false;
            var fit=authorHistoryFailureReason.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            authorHistoryFailureReason.rectTransform.anchorMin=new Vector2(0,1);authorHistoryFailureReason.rectTransform.anchorMax=Vector2.one;authorHistoryFailureReason.rectTransform.pivot=new Vector2(.5f,1);
            var contentFit=content.gameObject.AddComponent<ContentSizeFitter>();contentFit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;
            authorHistoryFailureSession=Label(card.transform,"author-history-session","",26,new Vector2(.025f,.22f),new Vector2(.975f,.34f),TextAnchor.MiddleLeft,Color.white);
            authorHistoryBack=LabButton(card.transform,"author-history-back","Назад",new Vector2(.025f,.06f),new Vector2(.29f,.18f),ToMainMenu);
            BindMenuShortcut(authorHistoryBack,MenuPadButton.East,()=>phase==Phase.Lab&&labDialog==null&&authorHistoryFailure.activeSelf);
            authorHistoryDefault=LabButton(card.transform,"author-history-default","Использовать Default из поставки",new Vector2(.32f,.06f),new Vector2(.975f,.18f),UsePackagedLabForSession);
            foreach(var button in new[]{authorHistoryBack,authorHistoryDefault})
            {button.GetComponentInChildren<Text>().fontSize=26;button.gameObject.AddComponent<LabErrorScrollNavigation>().Scroll=scroll;}
            var backNav=new Navigation{mode=Navigation.Mode.Explicit,selectOnRight=authorHistoryDefault};authorHistoryBack.navigation=backNav;
            authorHistoryDefault.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=authorHistoryBack};
            authorHistoryFailure.SetActive(false);
        }
        void UsePackagedLabForSession()
        {
            if(phase!=Phase.Lab||labProjectRoot==null||historyLoadError==null||IsLabHistoryLoading||authorSessionFallback)return;
            // Keep the failed author source and write guard. The trusted shipped snapshot
            // is a session-only choice; no repair, replacement or persisted selection.
            CacheLabIdentity(DesignLabHistory.ResolvePublishedDefault(historyStartupBaseline,historyStartupCatalogue));
            ApplySavedLabRevision();authorSessionFallback=true;authorStartupResolved=true;ToMainMenu();
        }
        void OpenDesignLab(){if(phase!=Phase.MainMenu)return;phase=Phase.Lab;if(IsLabHistoryReady){SetLabHistoryWaiting(false);ResetLabDraft();RefreshLabWorkspace();}else SetLabHistoryWaiting(true);labTreeScroll.verticalNormalizedPosition=1;labFieldsScroll.verticalNormalizedPosition=1;var group=labTree.Find("group-"+labGroup);if(group)RevealLabFocus(group.gameObject);RefreshInterface();Select(authorHistoryFailure.activeSelf?authorHistoryBack:labBack);}
        void SetLabHistoryWaiting(bool waiting)
        {
            bool authorFailure=waiting&&labProjectRoot!=null&&historyLoadError!=null;
            authorHistoryFailure.SetActive(authorFailure);
            foreach(var button in labScreen.GetComponentsInChildren<Button>(true))
                if(button!=labBack)button.interactable=!waiting;
            foreach(var inputField in labScreen.GetComponentsInChildren<InputField>(true))inputField.interactable=!waiting;
            if(authorFailure)
            {
                labBack.interactable=false;authorHistoryBack.interactable=true;authorHistoryDefault.interactable=!authorSessionFallback;
                authorHistoryFailureReason.text=historyLoadError+"\n\nФайл: DesignLab/authoring-history-v1.json\n\nВаши сохранённые значения, версии и релизные отметки не изменены.\nМожно вернуться к файлу проекта или продолжить на Default из поставки.";
                authorHistoryFailureSession.text=authorSessionFallback?"В этой сессии выбран Default из поставки. Запись авторской истории заблокирована.":"Временный выбор действует только в этой сессии. После перезапуска выбор повторяется.";
            }
            if(waiting)
            {
                ClearLabContent(labTree);ClearLabContent(labFields);labViews.Clear();
                labFieldsTitle.text=historyLoadError==null?"История Lab загружается…":"История Lab недоступна";
                labHint.text=historyLoadError??"Можно вернуться в меню и играть на Default. Сохранённая история проверяется в фоне.";
                labHint.color=historyLoadError==null?MenuGold:new Color32(230,125,118,255);
            }
        }
        void ResetLabDraft()
        {
            if(!IsLabHistoryReady)return;
            labBaseline=labHistory.Selected.Snapshot;labDraft=labBaseline.Clone();labRaw.Clear();labError=null;
            labRegistry=labDraft.Descriptors.Where(d=>labDraft.IsVisible(d.Path)).OrderBy(d=>GroupLabel(LabGroup(d)),StringComparer.Ordinal).ThenBy(d=>d.Path,StringComparer.Ordinal).ToArray();
        }
        IEnumerable<NumericDescriptor> LabDescriptors()=>labRegistry;
        string LabGroup(NumericDescriptor d)=>labDraft.IsAuthoring(d.Path)?"map-"+d.Group:d.Group;
        bool Changed(NumericDescriptor d)=>labRaw.ContainsKey(d.Path)||labDraft.Get(d.Path)!=labBaseline.Get(d.Path);
        void ClearLabContent(Transform parent){foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
        void PlaceLabRow(RectTransform rect,int index,float height)
        {rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(0,-(index+1)*height);rect.offsetMax=new Vector2(0,-index*height);}
        void RefreshLabWorkspace()
        {
            if(!IsLabHistoryReady){if(labScreen)SetLabHistoryWaiting(true);return;}if(labDraft==null)return;var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;string oldFocus=selected?selected.name:null;float treePosition=labTreeScroll.verticalNormalizedPosition,fieldPosition=labFieldsScroll.verticalNormalizedPosition;
            try{labParameterDefaults=labHistory.ReleaseParameterDefaults();labParameterDefaultsError=null;}
            catch(InvalidOperationException e){labParameterDefaults=null;labParameterDefaultsError=e.Message;}
            ClearLabContent(labTree);ClearLabContent(labFields);labViews.Clear();
            int i=0;
            foreach(var parentGroup in LabDescriptors().GroupBy(d=>GroupLabel(LabGroup(d)).Split('→')[0].Trim()))
            {
                var heading=Label(labTree,"parent-"+parentGroup.Key,parentGroup.Key+" · "+parentGroup.Count(Changed),16,Vector2.zero,Vector2.one,TextAnchor.MiddleLeft,MenuGold);PlaceLabRow(heading.rectTransform,i++,LabTreeRowHeight);
                foreach(var group in parentGroup.GroupBy(d=>LabGroup(d)))
                {
                    string key=group.Key;int count=group.Count(Changed);string label=GroupLabel(key).Split('→').Last().Trim();
                    var b=LabButton(labTree,"group-"+key,"   "+label+(count>0?" · "+count:""),Vector2.zero,Vector2.one,()=>{labGroup=key;labAllChanges=false;labQuery="";labSearch.SetTextWithoutNotify("");RefreshLabWorkspace();labFieldsScroll.verticalNormalizedPosition=1;});
                    PlaceLabRow((RectTransform)b.transform,i++,LabTreeRowHeight);b.GetComponentInChildren<Text>().fontSize=16;b.GetComponent<Image>().color=key==labGroup&&!labAllChanges?new Color32(48,78,92,255):MenuCard;b.GetComponentInChildren<Text>().color=key==labGroup&&!labAllChanges?MenuGold:Color.white;Hint(b.gameObject,GroupLabel(key)+" · полей "+group.Count()+" · правок "+count);
                }
            }
            ((RectTransform)labTree).sizeDelta=new Vector2(0,i*LabTreeRowHeight);
            var fields=LabDescriptors().Where(d=>labAllChanges?Changed(d):labQuery.Length>0?
                (GroupLabel(LabGroup(d))+" "+d.Group+" "+LabText(d.Label)+" "+LabText(d.Description)+" "+d.Path).IndexOf(labQuery,StringComparison.OrdinalIgnoreCase)>=0:LabGroup(d)==labGroup).ToArray();
            labFieldsTitle.text=labAllChanges?"Все изменения":labQuery.Length>0?"Результаты поиска · "+fields.Length:GroupLabel(labGroup);
            i=0;foreach(var d in fields)BuildLabField(d,i++);
            if(fields.Length==0)Label(labFields,"empty",labAllChanges?"Нет изменений":"Ничего не найдено",23,new Vector2(0,.7f),Vector2.one,TextAnchor.MiddleCenter,Color.white);
            ((RectTransform)labFields).sizeDelta=new Vector2(0,Mathf.Max(120,i*LabFieldRowHeight));
            Canvas.ForceUpdateCanvases();labTreeScroll.verticalNormalizedPosition=treePosition;labFieldsScroll.verticalNormalizedPosition=fieldPosition;
            RefreshLabValues();
            if(oldFocus!=null){var control=labScreen.GetComponentsInChildren<Selectable>().FirstOrDefault(c=>c.name==oldFocus&&c.interactable);if(control)SelectLabControl(control);else SelectLabControl(labSearch);}else SelectLabControl(labSearch);
        }
        void BuildLabField(NumericDescriptor d,int index)
        {
            var row=Panel(labFields,"field-"+d.Path,Vector2.zero,Vector2.one,MenuCard);PlaceLabRow((RectTransform)row.transform,index,LabFieldRowHeight);
            Label(row.transform,"label",LabText(d.Label),18,new Vector2(.02f,.76f),new Vector2(.56f,.98f),TextAnchor.MiddleLeft,Color.white);
            Label(row.transform,"description",GroupLabel(LabGroup(d))+" · "+LabText(d.Description),14,new Vector2(.02f,.36f),new Vector2(.56f,.75f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            bool boolean=d.Kind==LabParameterKind.Boolean;
            InputField input=null;Button down=null,up=null,booleanSwitch=null;
            if(boolean)
            {
                booleanSwitch=LabButton(row.transform,"bool-toggle-"+d.Path,"",new Vector2(.60f,.57f),new Vector2(.87f,.90f),()=>EditLabValue(d,labDraft.Get(d.Path)==0?1:0));
            }
            else
            {
                input=LabInput(row.transform,"input-"+d.Path,labRaw.TryGetValue(d.Path,out var raw)?raw:LabNumber(labDraft.Get(d.Path)),new Vector2(.64f,.57f),new Vector2(.82f,.90f));
                down=LabButton(row.transform,"minus-"+d.Path,"−",new Vector2(.58f,.57f),new Vector2(.63f,.90f),()=>EditLabValue(d,labDraft.Get(d.Path)-d.Step));
                up=LabButton(row.transform,"plus-"+d.Path,"+",new Vector2(.83f,.57f),new Vector2(.88f,.90f),()=>EditLabValue(d,labDraft.Get(d.Path)+d.Step));
            }
            var reset=LabButton(row.transform,"reset-"+d.Path,"Вернуть",new Vector2(.89f,.57f),new Vector2(.98f,.90f),()=>{labRaw.Remove(d.Path);labDraft.Set(d.Path,labBaseline.Get(d.Path));RefreshLabValues();});
            Label(row.transform,"unit",boolean?"Переключатель · Выкл / Вкл":d.Unit+" · "+LabNumber(d.Minimum)+"…"+LabNumber(d.Maximum)+" · шаг "+LabNumber(d.Step),14,new Vector2(.58f,.34f),new Vector2(.98f,.55f),TextAnchor.MiddleLeft,Color.white);
            var diff=Label(row.transform,"diff","",14,new Vector2(.02f,.17f),new Vector2(labAllChanges?.72f:.98f,.34f),TextAnchor.MiddleLeft,MenuGold);
            var error=Label(row.transform,"error","",14,new Vector2(.02f,.00f),new Vector2(labAllChanges?.72f:.98f,.17f),TextAnchor.MiddleLeft,new Color32(255,140,126,255));
            bool editable=!labHistory.SelectedProfileReadOnly&&labDraft.IsEditable(d.Path);
            foreach(var control in new Selectable[]{input,down,up,booleanSwitch,reset})if(control)control.interactable=editable;
            labViews[d.Path]=new LabFieldView{Input=input,Focus=boolean?(Selectable)booleanSwitch:input,Switch=booleanSwitch,Diff=diff,Error=error,Background=row.GetComponent<Image>()};
            if(input)input.onValueChanged.AddListener(value=>{if(labHistory.SelectedProfileReadOnly)return;labRaw[d.Path]=value;if(float.TryParse(value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out float n)&&!float.IsNaN(n)&&!float.IsInfinity(n)){labDraft.Set(d.Path,n);labRaw.Remove(d.Path);}RefreshLabValues(false);});
            string kind=d.Kind==LabParameterKind.Boolean?"Переключатель":d.Kind==LabParameterKind.Integer?"Целое число":"Дробное число";
            string releaseDefault=labParameterDefaults!=null?LabNumber(labParameterDefaults.Get(d.Path)):"недоступен · "+labParameterDefaultsError;
            string access=labHistory.SelectedProfileReadOnly?"Только чтение · профиль пакета · создайте копию":!editable?"Только чтение":labDraft.Domain(d.Path);
            string hint=access+" · "+LabText(d.Description)+"\n"+d.Path+" · "+kind+" · "+d.Unit+" · "+LabNumber(d.Minimum)+"…"+LabNumber(d.Maximum)+" · шаг "+LabNumber(d.Step)+" · Релизный Default: "+releaseDefault+
                "\nПрименение: "+LabApplicationLabel(d.Application)+(d.IsPreferenceDefault?" · личные настройки имеют приоритет":"");
            foreach(var control in new Selectable[]{input,down,up,booleanSwitch})if(control)Hint(control.gameObject,hint);
            Hint(reset.gameObject,hint+"\nВернуть значение выбранной сохранённой версии: "+LabNumber(labBaseline.Get(d.Path))+". Релизный Default не подставляется.");
            if(!editable){var info=LabButton(row.transform,"info-"+d.Path,"Описание",new Vector2(.80f,.02f),new Vector2(.98f,.20f),()=>labHint.text=hint);Hint(info.gameObject,hint);}
            if(labAllChanges){var jump=LabButton(row.transform,"jump-"+d.Path,"Открыть поле",new Vector2(.75f,.035f),new Vector2(.98f,.32f),()=>{labAllChanges=false;labGroup=LabGroup(d);RefreshLabWorkspace();SelectLabControl(labViews[d.Path].Focus);});Hint(jump.gameObject,"Открыть "+GroupLabel(LabGroup(d))+" и выбрать поле "+LabText(d.Label));}
        }
        static string LabApplicationLabel(LabParameterApplication application)
        {
            if(application==LabParameterApplication.NextGame)return "следующая игра после сохранения или выбора версии";
            throw new ArgumentOutOfRangeException(nameof(application));
        }
        void LabCheckState(Button button,bool selected,string caption)
        {
            var text=button.GetComponentInChildren<Text>();text.text=caption;text.alignment=TextAnchor.MiddleLeft;
            text.rectTransform.offsetMin=new Vector2(40,0);text.rectTransform.offsetMax=new Vector2(-6,0);
            var box=button.transform.Find("state-box");
            if(!box)
            {
                box=Panel(button.transform,"state-box",Vector2.zero,Vector2.one,MenuInk).transform;
                box.GetComponent<Image>().raycastTarget=false;var outline=box.gameObject.AddComponent<Outline>();outline.effectColor=new Color32(164,183,192,255);outline.effectDistance=Vector2.one;
                var rect=(RectTransform)box;rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);rect.pivot=new Vector2(0,.5f);rect.anchoredPosition=new Vector2(10,0);rect.sizeDelta=new Vector2(22,22);
                var check=Label(box,"state-check","✓",20,Vector2.zero,Vector2.one,TextAnchor.MiddleCenter,MenuGold);check.raycastTarget=false;check.verticalOverflow=VerticalWrapMode.Overflow;
            }
            box.Find("state-check").gameObject.SetActive(selected);
        }
        static string LabCompactName(string name)=>name.Length>24?name.Substring(0,21)+"…":name;
        static string LabNumber(float n)=>n.ToString("0.#####",CultureInfo.InvariantCulture);
        void EditLabValue(NumericDescriptor d,float n){if(labHistory.SelectedProfileReadOnly)return;labRaw.Remove(d.Path);labDraft.Set(d.Path,(float)Math.Round(n,6));RefreshLabValues();}
        List<ProfileValidationIssue> LabIssues()=>labDraft.Validate().Concat(labRaw.Keys.Select(p=>new ProfileValidationIssue{Path=p,Message="Введите конечное число"})).ToList();
        void RefreshLabValues(bool rewrite=true)
        {
            var issues=LabIssues();var baseline=labBaseline;
            foreach(var pair in labViews)
            {
                string path=pair.Key;var view=pair.Value;float old=baseline.Get(path),now=labDraft.Get(path);bool changed=labRaw.ContainsKey(path)||now!=old;
                if(view.Switch)LabCheckState(view.Switch,now==1,now==1?"Вкл":"Выкл");
                if(rewrite&&view.Input)view.Input.SetTextWithoutNotify(labRaw.TryGetValue(path,out var raw)?raw:LabNumber(now));
                view.Diff.text=!labDraft.IsEditable(path)?"ТОЛЬКО ЧТЕНИЕ · "+CombatBowlCatalog.Identity+" · для изменения требуется новая map revision":changed?"ИЗМЕНЕНО · "+LabNumber(old)+" → "+(labRaw.TryGetValue(path,out var text)?text:LabNumber(now))+" · Δ "+(now-old>=0?"+":"")+LabNumber(now-old):"Без изменений";
                view.Error.text=string.Join("; ",issues.Where(e=>e.Path==path).Select(e=>e.Message));view.Background.color=changed?new Color32(42,63,61,255):MenuCard;
            }
            foreach(Transform row in labTree)
            {
                if(row.name.StartsWith("group-")){string group=row.name.Substring(6);int count=LabDescriptors().Count(d=>LabGroup(d)==group&&Changed(d));row.GetComponentInChildren<Text>().text="   "+GroupLabel(group).Split('→').Last().Trim()+(count>0?" · "+count:"");}
                else {string parent=row.name.Substring(7);row.GetComponent<Text>().text=parent+" · "+LabDescriptors().Count(d=>GroupLabel(LabGroup(d)).StartsWith(parent+" →",StringComparison.Ordinal)&&Changed(d));}
            }
            bool readOnly=labHistory.SelectedProfileReadOnly;labRename.interactable=!readOnly&&labHistory.Writable;labCreate.GetComponentInChildren<Text>().text="Создать копию";
            labSave.interactable=!readOnly&&(LabDirty||labHistory.CanMaterializeSelected)&&issues.Count==0&&labHistory.Writable;labDelete.interactable=!labHistory.SelectedProfileProtected&&labHistory.Writable;
            var selected=labHistory.Selected;bool shipped=!labHistory.IsAuthoring&&labHistory.IsShipped(selected);
            labRelease.interactable=!readOnly&&!LabDirty&&!shipped&&labHistory.Compatible(selected)&&labHistory.Writable;
            LabCheckState(labRelease,labHistory.SelectedReleaseMarked,"В релиз");
            labReleaseReason.text=shipped?"Профиль пакета · только чтение":readOnly?"Только чтение":LabDirty?"Сначала сохраните правки":!labHistory.Writable?"Запись недоступна":!labHistory.Compatible(selected)?"Версия несовместима":labHistory.IsAuthoring?"Отметка для следующего выпуска":"Локальная отметка · без выпуска";
            string name=labHistory.SelectedProfileName;labProfileSelector.GetComponentInChildren<Text>().text=(name.Length>24?name.Substring(0,21)+"…":name)+" ▾";labRevisionSelector.GetComponentInChildren<Text>().text=LabRevisionLabel(labHistory,selected)+" ▾";
            labDefaultSource.text=labHistory.ReleaseError!=null?"Default недоступен · проверьте отметки":labHistory.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Name+" · последняя релизная версия";
            labIdentity.fontSize=16;
            labIdentity.text=(labHistory.IsAuthoring?"Файл проекта: DesignLab/authoring-history-v1.json":"Данные игрока · "+(labHistory.SelectedProfileProtected?"профиль пакета защищён":"свой профиль"))+"\n"+
                (labProjectRoot!=null&&!authorStartupResolved?"Стартовая версия проекта не выбрана":"Для следующей игры: "+labActiveLabel);
            labStatus.text=labError??labHistory.StorageError??labHistory.ReleaseError??(readOnly?"Только чтение · для правок создайте копию":issues.Count>0?"Ошибок: "+issues.Count+" · сохранение заблокировано":LabDirty?"Черновик · правок "+LabDescriptors().Count(Changed):labWriteMessage??(labHistory.CanMaterializeSelected?"Можно сохранить полную новую версию":"Сохранённая ревизия · нет изменений"));
        }
        IEnumerator SelectLabRevision(string profile,int revision)
        {
            if(labHistory.Busy)yield break;
            var card=LabDialog("Выбор ревизии…");
            var cancel=LabCancelButton(card,"cancel","Назад",new Vector2(.70f,.10f),new Vector2(.95f,.185f),CloseLabDialog);
            cancel.interactable=false;
            Task operation=labHistory.SelectAsync(profile,revision);
            while(!operation.IsCompleted)yield return null;
            if(cancel){cancel.interactable=true;Select(cancel);}
            LabOperation(()=>operation.GetAwaiter().GetResult(),true);
        }
        void LabOperation(Action action,bool reset,bool activate=true)
        {
            if(!IsLabHistoryReady||labHistory.Busy)return;
            try
            {
                action();
                if(activate){CacheLabIdentity();if(labHistory.IsAuthoring)authorStartupResolved=true;}
                else if(activeLabReference?.ProfileId==labHistory.SelectedProfileId&&activeLabReference.Hash==labHistory.Selected.Hash)CacheLabIdentity();
                if(reset)ResetLabDraft();
                if(labHistory.IsAuthoring)labWriteMessage="Файл проекта записан · Git commit/push не выполнялся";
                labError=null;CloseLabDialog();RefreshLabWorkspace();
            }
            catch(Exception e)
            {
                labError=e.Message;
                if(labDialog!=null)
                {
                    var card=labDialog.transform.Find("dialog-card");var previous=card.Find("operation-error");
                    if(previous)previous.GetComponent<Text>().text=labError;
                    else Label(card,"operation-error",labError,18,new Vector2(.05f,.68f),new Vector2(.95f,.76f),TextAnchor.MiddleLeft,new Color32(255,140,126,255));
                }
                else RefreshLabValues();
            }
        }
        void CloseLabDialog()
        {
            if(labHistory!=null&&labHistory.Busy)return;
            if(labDialog==null&&labDialogControls.Count==0)return;
            if(labDialog!=null){labDialog.SetActive(false);Destroy(labDialog);labDialog=null;}labCancelAction=null;
            foreach(var pair in labDialogControls)if(pair.Key)pair.Key.interactable=pair.Value;labDialogControls.Clear();
            if(EventSystem.current&&labDialogReturn&&labDialogReturn.activeInHierarchy)EventSystem.current.SetSelectedGameObject(labDialogReturn);else Select(labBack);labDialogReturn=null;if(labDraft!=null)RefreshLabValues();
        }
        Transform LabDialog(string title)
        {
            var previous=labDialog!=null?labDialogReturn:(EventSystem.current?EventSystem.current.currentSelectedGameObject:null);
            CloseLabDialog();labDialogReturn=previous;
            foreach(var control in labScreen.GetComponentsInChildren<Selectable>()){labDialogControls[control]=control.interactable;control.interactable=false;}
            labDialog=Panel(labScreen.transform,"lab-dialog",Vector2.zero,Vector2.one,SelectorBackdrop);
            labDialog.transform.SetAsLastSibling();
            var card=Panel(labDialog.transform,"dialog-card",new Vector2(.22f,.27f),new Vector2(.78f,.73f),MenuCard);StyleSelectorCard(card,false);
            Label(card.transform,"title",title,28,new Vector2(.05f,.76f),new Vector2(.95f,.96f),TextAnchor.MiddleLeft,MenuGold);return card.transform;
        }
        void LabConfirm(string title,string confirm,Action action)
        {var card=LabDialog(title);var cancel=LabCancelButton(card,"cancel","Продолжить редактирование",new Vector2(.05f,.10f),new Vector2(.55f,.185f),CloseLabDialog);LabButton(card,"confirm",confirm,new Vector2(.60f,.10f),new Vector2(.95f,.185f),()=>action());Select(cancel);}
        void ConfirmLabDiscard(Action next)
        {
            var card=LabDialog("Отбросить несохранённые изменения?");
            Layout((RectTransform)card,new Vector2(.12f,.14f),new Vector2(.88f,.80f));
            Layout((RectTransform)card.Find("title"),new Vector2(.04f,.87f),new Vector2(.96f,.99f));
            card.Find("title").GetComponent<Text>().fontSize=28;
            Label(card,"discard-saved-version","Сохранённая версия "+LabRevisionLabel(labHistory,labHistory.Selected)+" и история профиля останутся без изменений.",20,
                new Vector2(.04f,.78f),new Vector2(.96f,.86f),TextAnchor.MiddleLeft,Color.white);
            var scroll=LabScroll(card,"discard-changes",new Vector2(.04f,.30f),new Vector2(.96f,.76f),out var list);
            int index=0;
            foreach(var descriptor in LabDescriptors().Where(Changed))
            {
                var row=LabButton(list,"discard-change-"+descriptor.Path,"",Vector2.zero,Vector2.one,()=>{});
                PlaceLabRow((RectTransform)row.transform,index++,76);
                Label(row.transform,"field",GroupLabel(LabGroup(descriptor))+" · "+LabText(descriptor.Label),20,
                    new Vector2(.02f,.04f),new Vector2(.64f,.96f),TextAnchor.MiddleLeft,Color.white);
                string value=labRaw.TryGetValue(descriptor.Path,out var raw)?raw:LabNumber(labDraft.Get(descriptor.Path));
                var values=Label(row.transform,"values",LabNumber(labBaseline.Get(descriptor.Path))+" → "+value+" "+descriptor.Unit,20,
                    new Vector2(.66f,.04f),new Vector2(.98f,.96f),TextAnchor.MiddleRight,MenuGold);
                values.resizeTextForBestFit=true;values.resizeTextMinSize=14;values.resizeTextMaxSize=20;
                var focus=row.GetComponent<MenuScrollFocus>()??row.gameObject.AddComponent<MenuScrollFocus>();
                focus.Scroll=scroll;focus.Row=(RectTransform)row.transform;
            }
            ((RectTransform)list).sizeDelta=new Vector2(0,index*76);scroll.verticalNormalizedPosition=1;
            Label(card,"discard-consequences","Несохранённые изменения будут потеряны.",17,
                new Vector2(.04f,.20f),new Vector2(.96f,.28f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            var cancel=LabCancelButton(card,"cancel","Продолжить редактирование",new Vector2(.04f,.07f),new Vector2(.52f,.17f),CloseLabDialog);
            cancel.GetComponentInChildren<Text>().fontSize=20;
            var confirm=LabButton(card,"confirm","Отбросить и продолжить",new Vector2(.54f,.07f),new Vector2(.96f,.17f),()=>{ResetLabDraft();CloseLabDialog();next();});
            confirm.GetComponentInChildren<Text>().fontSize=20;
            Select(cancel);
        }
        void GuardLabSelection(Action action){if(LabDirty)ConfirmLabDiscard(action);else action();}
        void ShowLabErrors()
        {
            var issues=LabIssues();var card=LabDialog(issues.Count==0?"Нет ошибок":"Ошибки черновика");var scroll=LabScroll(card,"error-options",new Vector2(.05f,.17f),new Vector2(.95f,.75f),out var list);int i=0;
            foreach(var issue in issues){string path=issue.Path;var b=LabButton(list,"error-"+path,path+" · "+issue.Message,Vector2.zero,Vector2.one,()=>{CloseLabDialog();labQuery=path;labAllChanges=false;labSearch.SetTextWithoutNotify(path);RefreshLabWorkspace();if(labViews.TryGetValue(path,out var field))SelectLabControl(field.Focus);});PlaceLabRow((RectTransform)b.transform,i++,50);b.GetComponentInChildren<Text>().fontSize=16;}
            ((RectTransform)list).sizeDelta=new Vector2(0,i*50);var cancel=LabCancelButton(card,"cancel","Назад",new Vector2(.70f,.06f),new Vector2(.95f,.145f),CloseLabDialog);Select(cancel);
        }
        Transform LabDropdown(Button source)
        {
            var card=LabDialog("ВЫБЕРИТЕ ПРОФИЛЬ");labDialog.GetComponent<Image>().color=SelectorBackdrop;
            Layout((RectTransform)card,new Vector2(.30f,.18f),new Vector2(.70f,.82f));StyleSelectorCard(card.gameObject);
            var title=card.Find("title").GetComponent<Text>();title.fontSize=28;title.alignment=TextAnchor.MiddleCenter;
            Layout(title.rectTransform,new Vector2(.05f,.85f),new Vector2(.95f,.98f));return card;
        }
        void ShowLabProfiles()
        {
            var card=LabDropdown(labProfileSelector);var scroll=LabScroll(card,"profile-options",new Vector2(.05f,.19f),new Vector2(.95f,.82f),out var list);int i=0;Button first=null;
            foreach(var profile in labHistory.Profiles)
            {
                var p=profile;var b=LabButton(list,"profile-"+p.Id,p.Name,Vector2.zero,Vector2.one,()=>
                {
                    int number=labHistory.LatestAvailableRevision(p.Id);
                    if(labHistory.Compatible(p.Revisions.Single(r=>r.Number==number)))GuardLabSelection(()=>StartCoroutine(SelectLabRevision(p.Id,number)));
                    else InspectLabRevision(number,true,p.Id); // Viewing never discards or activates a draft.
                });
                PlaceLabRow((RectTransform)b.transform,i++,56);b.GetComponentInChildren<Text>().fontSize=22;b.GetComponent<MenuPresentation>().FilledFocus=true;first=first??b;
            }
            ((RectTransform)list).sizeDelta=new Vector2(0,i*56);var back=LabCancelButton(card,"cancel","‹ НАЗАД",new Vector2(.05f,.04f),new Vector2(.95f,.14f),CloseLabDialog);back.GetComponentInChildren<Text>().fontSize=22;if(first!=null)Select(first);
        }
        void ShowLabRevisions()=>InspectLabRevision(labHistory.Selected.Number,true);
        void InspectLabRevision(int number,bool focusList=false,string profileId=null)
        {
            profileId=profileId??labHistory.SelectedProfileId;
            var profile=labHistory.Profiles.Single(p=>p.Id==profileId);
            var revision=profile.Revisions.Single(r=>r.Number==number);
            bool selectedProfile=profileId==labHistory.SelectedProfileId;
            var card=LabDialog("СОХРАНЁННЫЕ ВЕРСИИ");labDialog.GetComponent<Image>().color=SelectorBackdrop;StyleSelectorCard(card.gameObject,false);
            Layout((RectTransform)card,new Vector2(.04f,.08f),new Vector2(.96f,.90f));
            Layout((RectTransform)card.Find("title"),new Vector2(.03f,.87f),new Vector2(.97f,.98f));
            var scroll=LabScroll(card,"revision-options",new Vector2(.03f,.16f),new Vector2(.36f,.85f),out var list);int i=0;Button inspected=null;
            var revisions=profile.Revisions;
            foreach(var r in revisions.Where(r=>labHistory.IsAuthoring||labHistory.Compatible(r)||!labHistory.IsShipped(r)||!revisions.Any(other=>other.ReleaseSequence==r.ReleaseSequence&&labHistory.Compatible(other))).OrderByDescending(r=>r.Number))
            {
                int n=r.Number;bool selected=n==number;
                var row=LabButton(list,"revision-"+n,LabRevisionLabel(labHistory,r,profileId)+(r.IsCompatibilityView?" · для текущей игры":"")+(selectedProfile&&r.Number==labHistory.Selected.Number?" · выбранная база":"")+(labHistory.IsMarkedForRelease(profileId,r.Number)?" · релизная":"")+(labHistory.Compatible(r)?"":" · прежняя схема"),Vector2.zero,Vector2.one,()=>InspectLabRevision(n,false,profileId));
                PlaceLabRow((RectTransform)row.transform,i++,64);row.GetComponentInChildren<Text>().fontSize=20;
                if(selected){row.GetComponent<Image>().color=MenuGold;row.GetComponentInChildren<Text>().color=MenuInk;inspected=row;}
                var focus=row.gameObject.AddComponent<MenuScrollFocus>();focus.Scroll=scroll;focus.Row=(RectTransform)row.transform;
            }
            ((RectTransform)list).sizeDelta=new Vector2(0,i*64);
            card.Find("title").GetComponent<Text>().fontSize=28;
            var detail=Panel(card,"revision-detail",new Vector2(.39f,.16f),new Vector2(.97f,.85f),MenuInk).transform;
            Label(detail,"revision-identity",profile.Name+"\n"+LabRevisionLabel(labHistory,revision,profileId),28,new Vector2(.04f,.76f),new Vector2(.96f,.96f),TextAnchor.MiddleLeft,MenuGold);
            bool compatible=labHistory.Compatible(revision);
            string status=(selectedProfile&&revision.Number==labHistory.Selected.Number?"Выбранная сохранённая база":"Просмотр версии · база и черновик не меняются")+"\n"+
                (labHistory.IsMarkedForRelease(profileId,number)?"Отмечена для релиза":"Без релизной отметки")+"\n"+
                (compatible?(revision.IsCompatibilityView?"Представление исходной версии для текущей игры":"Доступна для следующей игры"):
                    labHistory.CompatibilityReason(profileId,number)+". Выбор для игры недоступен; исходная версия сохранена.");
            var statusText=Label(detail,"revision-status",status,24,new Vector2(.04f,.39f),new Vector2(.96f,.75f),TextAnchor.UpperLeft,Color.white);statusText.supportRichText=false;
            var hash=Label(detail,"revision-hash","SHA256 "+labHistory.SavedIdentity(profileId,number).Hash,14,new Vector2(.04f,.28f),new Vector2(.96f,.39f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));hash.supportRichText=false;
            var mark=LabButton(detail,"revision-release","В релиз",new Vector2(.04f,.14f),new Vector2(.96f,.25f),()=>
            {
                try
                {
                    labHistory.MarkForRelease(profileId,number,!labHistory.IsMarkedForRelease(profileId,number));
                    labWriteMessage="Файл проекта записан · Git commit/push не выполнялся";labError=null;
                    CloseLabDialog();RefreshLabWorkspace();InspectLabRevision(number,false,profileId);
                }
                catch(Exception e){statusText.text=e.Message;}
            });
            bool marked=labHistory.IsMarkedForRelease(profileId,number);
            LabCheckState(mark,marked,"В релиз");mark.GetComponentInChildren<Text>().fontSize=22;
            mark.interactable=labHistory.IsAuthoring&&labHistory.Writable&&(compatible||marked);
            statusText.text+="\n"+(!labHistory.IsAuthoring?"Отметки проекта доступны автору":!compatible?(marked?"Можно только снять отметку. Включить снова нельзя.":"Включение недоступно: версия несовместима."):"Отметка не меняет базу. Повторное нажатие снимает её.");
            var choose=LabButton(detail,"revision-select","Выбрать",new Vector2(.04f,.01f),new Vector2(.96f,.12f),()=>GuardLabSelection(()=>StartCoroutine(SelectLabRevision(profileId,number))));
            choose.interactable=compatible;
            Label(card,"revision-active","Для следующей игры: "+labActiveLabel,20,new Vector2(.03f,.035f),new Vector2(.69f,.12f),TextAnchor.MiddleLeft,Color.white);
            var close=LabCancelButton(card,"cancel","Закрыть",new Vector2(.72f,.035f),new Vector2(.97f,.12f),CloseLabDialog);
            close.GetComponentInChildren<Text>().fontSize=20;choose.GetComponentInChildren<Text>().fontSize=22;
            if(focusList&&inspected)SelectLabControl(inspected);else Select(close);
            if(inspected)inspected.GetComponent<MenuScrollFocus>().Reveal();
        }
        void LabNameDialog(bool rename)
        {
            var card=LabDialog(rename?"Переименовать профиль":"НОВЫЙ ПРОФИЛЬ");
            if(!rename)
            {
                Layout((RectTransform)card,new Vector2(.15f,.16f),new Vector2(.85f,.80f));
                card.Find("title").GetComponent<Text>().fontSize=40;
                Label(card,"copy-heading","Копия сохранённой версии",34,new Vector2(.05f,.72f),new Vector2(.95f,.83f),TextAnchor.MiddleLeft,MenuGold);
                var source=Label(card,"copy-source",labHistory.SelectedProfileName+" · "+LabRevisionLabel(labHistory,labHistory.Selected),32,new Vector2(.05f,.58f),new Vector2(.95f,.72f),TextAnchor.MiddleLeft,Color.white);source.supportRichText=false;
                Label(card,"copy-name-label","Имя профиля",28,new Vector2(.05f,.48f),new Vector2(.95f,.56f),TextAnchor.MiddleLeft,Color.white);
                Label(card,"copy-explanation","Несохранённые правки не копируются. Если есть правки, перед созданием появится подтверждение их потери.\nУ копии будет своя история. Релизная отметка не переносится.",26,new Vector2(.05f,.19f),new Vector2(.95f,.34f),TextAnchor.MiddleLeft,Color.white);
            }
            var name=LabInput(card,"lab-profile-name",rename?labHistory.SelectedProfileName:"Новый профиль",new Vector2(.05f,rename?.52f:.36f),new Vector2(.95f,rename?.605f:.47f));
            var submit=LabButton(card,"submit",rename?"Переименовать":"Создать",new Vector2(rename?.60f:.55f,rename?.15f:.06f),new Vector2(.95f,rename?.235f:.16f),()=>{string value=name.text;if(rename)LabOperation(()=>labHistory.Rename(value),false,false);else GuardLabSelection(()=>LabOperation(()=>labHistory.Create(value),true));});
            var cancel=LabCancelButton(card,"cancel","Отмена",new Vector2(.05f,rename?.15f:.06f),new Vector2(rename?.55f:.45f,rename?.235f:.16f),CloseLabDialog);
            if(!rename){name.textComponent.fontSize=26;submit.GetComponentInChildren<Text>().fontSize=26;cancel.GetComponentInChildren<Text>().fontSize=26;}
            SelectLabControl(name);
        }
        void UpdateDesignLab()
        {
            if(phase!=Phase.Lab||labHistory?.Busy==true||!TryTakeMenuBack())return;
            if(labDialog!=null)
            {if(labCancelAction&&labCancelAction.IsInteractable())labCancelAction.onClick.Invoke();else CloseLabDialog();}
            else if(authorHistoryFailure.activeSelf)authorHistoryBack.onClick.Invoke();
            else labBack.onClick.Invoke();
        }
    }
    sealed class LabErrorScrollNavigation : MonoBehaviour, IMoveHandler
    {
        public ScrollRect Scroll;
        public void OnMove(AxisEventData data)
        {
            if(data.moveDir!=MoveDirection.Up&&data.moveDir!=MoveDirection.Down)return;
            Scroll.verticalNormalizedPosition=Mathf.Clamp01(Scroll.verticalNormalizedPosition+(data.moveDir==MoveDirection.Up?.25f:-.25f));data.Use();
        }
    }
}

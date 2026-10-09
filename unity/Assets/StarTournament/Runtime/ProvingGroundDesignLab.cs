using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        Text labIdentity,labStatus,labHint,labFieldsTitle; Button labSave,labDelete,labBack,labProfileSelector,labRevisionSelector,labRelease,labCreate,labRename;
        InputField labSearch; GameObject labDialog; string labGroup="rifle",labQuery="",labError;
        GameObject labDialogReturn; readonly Dictionary<Selectable,bool> labDialogControls=new Dictionary<Selectable,bool>();
        bool labAllChanges; readonly Dictionary<string,string> labRaw=new Dictionary<string,string>();
        readonly Dictionary<string,LabFieldView> labViews=new Dictionary<string,LabFieldView>();
        sealed class LabFieldView {public InputField Input;public Text Diff,Error;public Image Background;}
        public string FrozenLabIdentity {get;private set;}
        LabRevisionReference frozenLabReference;
        string labSavedIdentity="",labSavedRevisionLabel="";
        NumericDescriptor[] labRegistry=Array.Empty<NumericDescriptor>();
        public string LabSavedIdentity=>labSavedIdentity;
        public bool LabSelectionPending=>labHistory!=null&&labHistory.Busy;
        void CacheLabIdentity()
        {
            var revision=labHistory.Selected;
            labSavedRevisionLabel=revision.Label;
            labSavedIdentity=labHistory.SelectedProfileName+" · "+revision.Label+"\n"+labHistory.SelectedProfileId+"\nSHA256 "+revision.Hash;
        }
        bool LabDirty=>labDraft!=null&&(labRaw.Count>0||labDraft.Hash()!=labBaseline.Hash());
        LabBundle CurrentLabBundle()=>new LabBundle{Profiles=new List<ProvingProfile>{Profile,LifeProfile,CombatProfile,TrooperProfile,MatchProfile,TeamProfile,RosterProfile,BotPerceptionProfile,BotNavigationProfile,BotBehaviorProfile,OrbitalLeagueProfile,CombatBowlAuthoring,CutterProfile,ParticipantPaletteProfile,ProvingProfile.CreateBotEvaluationDefault(),DeathProfile,BloodProfile,RocketEffectsProfile,TunnelsPresentation,TunnelsAuthoring,LunarPresentation,LunarAuthoring}};
        public LabBundle CaptureLabBundle()=>CurrentLabBundle().Clone();
        void InitializeDesignLab()
        {
            OrbitalLeagueProfile.EnsureOrbitalLeagueDescriptors();
            BotBehaviorProfile.EnsureBotDescriptors();BotPerceptionProfile.EnsureBotDescriptors();
            // Registry v2 adds heal fields and replaces the authored basement armor paths.
            // Keep v1 histories and their immutable hashes intact; v2 starts from the new shipped baseline.
            string historyPath=Path.Combine(Application.persistentDataPath,"design-lab-history-v2.json");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY")))historyPath=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_LAB_HISTORY");
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-labReview");
            if(flag>=0&&flag+1<args.Length)historyPath=Path.Combine(args[flag+1],"history.json");
            int colors=Array.IndexOf(args,"-colorIdentityEvidence");
            if(Array.IndexOf(args,"-colorIdentityReview")>=0&&colors>=0&&colors+1<args.Length)
                historyPath=Path.Combine(args[colors+1],"qa-lab-history.json");
#endif
            var startupWatch=System.Diagnostics.Stopwatch.StartNew();
            Debug.Log("Lab startup: validating history in memory");
            // Startup selects the packaged Default; compatibility snapshots are validated
            // in memory. Persist them with the next explicit Lab mutation, not a full history
            // rewrite while the splash screen blocks the player.
            labHistory=new DesignLabHistory(historyPath,CurrentLabBundle(),releases:LabReleaseCatalog.Load(),resetToLatestDefault:true,persistMigration:false);
            Debug.Log("Lab startup: history ready in "+startupWatch.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture)+" seconds");
            Application.wantsToQuit+=ProtectLabQuit;
            labBaseline=labHistory.Selected.Snapshot;labDraft=labBaseline.Clone();CacheLabIdentity();ApplySavedLabRevision();
        }
        bool ProtectLabQuit()
        {
            if(labHistory!=null&&labHistory.Busy)return false;
            if(phase!=Phase.Lab||!LabDirty)return true;
            ConfirmLabDiscard(()=>Application.Quit());return false;
        }
        void ApplySavedLabRevision()
        {
            if(labHistory==null)return;var saved=labHistory.Selected.Snapshot;
            // Runtime profile objects retain their identity for fixture/authoring references.
            // Match creation copies their values into separate frozen owners.
            foreach(var profile in CurrentLabBundle().Profiles)
                foreach(var descriptor in profile.Descriptors)profile.Set(descriptor.Path,saved.Get(descriptor.Path));
        }
        static string GroupLabel(string group)
        {
            switch(group)
            {
                case "tunnel-light":return "Тоннели → Свет";case "tunnel-surface":return "Тоннели → Поверхности";
                case "map-geometry":return "Карта → Геометрия (read-only)";case "map-spawn":return "Карта → Спавны (read-only)";case "map-transitions":return "Карта → Переходы (read-only)";case "map-routes":return "Карта → Маршруты (read-only)";case "map-bonuses":return "Карта → Бонусы (read-only)";
                case "cutter-effects":return "Оружие → Резак · оформление";case "cutter-bots":return "Боты → Резак";case "cutter":return "Оружие → Резак";case "rocket-effects":return "Оружие → Pulse · оформление";case "rocket":return "Оружие → Pulse";case "rifle":return "Оружие → Винтовка";case "shotgun":return "Оружие → Дробовик";case "weapon-switch":return "Оружие → Переключение";
                case "weapon-pickup":return "Бонусы → Оружие";case "full-heal":return "Бонусы → Полное исцеление";case "armor-pickup":return "Бонусы → Броня";case "damage-boost":return "Бонусы → Урон";case "speed-pickup":return "Бонусы → Скорость";
                case "pickups":return "Бонусы → Подбор";case "combat":return "Игрок → Здоровье";case "motor":return "Игрок → Движение";
                case "capsule":return "Игрок → Капсула";case "camera":return "Камера → Вид игрока";case "input":return "Игрок → Управление";
                case "death":return "Смерть → Тело";case "spawn":return "Смерть → Возрождение";case "killcam":return "Камера → Killcam";
                case "bot-evaluation":return "Боты → Контрольная оценка (read-only)";case "bots":return "Боты → Восприятие";case "bot-navigation":return "Боты → Навигация";case "bot-behavior":return "Боты → Поведение";
                case "match":return "Матч → Правила";case "score":return "Матч → Очки";case "teams":return "Матч → Команды";
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
        InputField LabInput(Transform parent,string name,string text,Vector2 min,Vector2 max)
        {
            var panel=Panel(parent,name,min,max,new Color32(35,51,68,255));var input=panel.AddComponent<InputField>();
            input.textComponent=Label(panel.transform,"value",text,16,new Vector2(.04f,0),new Vector2(.96f,1),TextAnchor.MiddleLeft,Color.white);
            input.text=text;return input;
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
            labBack=LabButton(labScreen.transform,"lab-back","‹ Назад",new Vector2(.05f,.91f),new Vector2(.15f,.96f),ToMainMenu);
            Label(labScreen.transform,"lab-heading","ЛАБОРАТОРИЯ ГЕЙМДИЗАЙНА",21,new Vector2(.18f,.91f),new Vector2(.75f,.96f),TextAnchor.MiddleLeft,MenuGold);
            var clear=LabButton(labScreen.transform,"lab-clear","Очистить",new Vector2(.46f,.785f),new Vector2(.56f,.83f),()=>{ResetLabDraft();RefreshLabWorkspace();});
            labSave=LabButton(labScreen.transform,"lab-save","Сохранить",new Vector2(.34f,.785f),new Vector2(.45f,.83f),()=>LabOperation(()=>labHistory.Save(labDraft),true));
            labSave.GetComponent<Image>().color=MenuGold;labSave.GetComponentInChildren<Text>().color=MenuInk;
            labHint=Label(labScreen.transform,"lab-hint","",14,new Vector2(.02f,.005f),new Vector2(.98f,.055f),TextAnchor.MiddleLeft,MenuGold);
            Hint(clear.gameObject,"Очистить: вернуть все поля к выбранной базе; история сохраняется.");Hint(labSave.gameObject,"Сохранить: добавить новую неизменяемую ревизию. Нужен изменённый черновик без ошибок.");
            labProfileSelector=LabButton(labScreen.transform,"lab-profile-select","Профиль ▾",new Vector2(.02f,.85f),new Vector2(.32f,.895f),ShowLabProfiles);
            labRevisionSelector=LabButton(labScreen.transform,"lab-revision-select","Ревизия ▾",new Vector2(.02f,.785f),new Vector2(.32f,.83f),ShowLabRevisions);
            labCreate=LabButton(labScreen.transform,"lab-create","Создать",new Vector2(.34f,.85f),new Vector2(.44f,.895f),()=>LabNameDialog(false));
            labRename=LabButton(labScreen.transform,"lab-rename","Переименовать",new Vector2(.45f,.85f),new Vector2(.59f,.895f),()=>LabNameDialog(true));
            labDelete=LabButton(labScreen.transform,"lab-delete","Удалить",new Vector2(.60f,.85f),new Vector2(.70f,.895f),()=>LabConfirm("Удалить «"+labHistory.SelectedProfileName+"»?\nРевизий: "+labHistory.SelectedProfile.Revisions.Count,"Удалить профиль",()=>LabOperation(labHistory.DeleteSelected,true)));
            Hint(labDelete.gameObject,"Удаляется целый локальный профиль после подтверждения. Профили со встроенными релизами защищены от удаления.");
            LabButton(labScreen.transform,"lab-changes","Все изменения",new Vector2(.57f,.785f),new Vector2(.71f,.83f),()=>{labAllChanges=true;labQuery="";labSearch.SetTextWithoutNotify("");RefreshLabWorkspace();});
            labIdentity=Label(labScreen.transform,"lab-identity","",12,new Vector2(.02f,.72f),new Vector2(.98f,.745f),TextAnchor.MiddleLeft,Color.white);
            labRelease=LabButton(labScreen.transform,"lab-release","Релизная: нет",new Vector2(.82f,.785f),new Vector2(.98f,.83f),()=>LabOperation(()=>labHistory.MarkSelectedForRelease(!labHistory.Selected.ReleaseCandidate),false));
            Hint(labRelease.gameObject,"Отметить сохранённую ревизию для будущего выпуска. Отметка локальная; выпуск выполняется отдельно. Правки сначала сохраните.");
            labSearch=LabInput(labScreen.transform,"lab-search","",new Vector2(.02f,.735f),new Vector2(.47f,.775f));
            labSearch.placeholder=Label(labSearch.transform,"search-placeholder","Поиск по названию, описанию или пути…",16,new Vector2(.04f,0),new Vector2(.96f,1),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            Hint(labSearch.gameObject,"Поиск во всех сущностях: название, влияние, группа или stable path. Очистите для возврата к выбранной сущности.");
            labSearch.onValueChanged.AddListener(q=>{labQuery=q;labAllChanges=false;RefreshLabWorkspace();});
            labStatus=Label(labScreen.transform,"lab-status","",14,new Vector2(.49f,.735f),new Vector2(.98f,.775f),TextAnchor.MiddleLeft,MenuGold);
            LabButton(labScreen.transform,"lab-errors","Ошибки",new Vector2(.72f,.785f),new Vector2(.81f,.83f),ShowLabErrors);
            labTreeScroll=LabScroll(labScreen.transform,"lab-tree",new Vector2(.02f,.065f),new Vector2(.22f,.70f),out labTree);
            labFieldsTitle=Label(labScreen.transform,"lab-fields-title","",18,new Vector2(.24f,.695f),new Vector2(.98f,.718f),TextAnchor.MiddleLeft,MenuGold);
            labFieldsScroll=LabScroll(labScreen.transform,"lab-fields",new Vector2(.24f,.065f),new Vector2(.98f,.69f),out labFields);
        }
        void OpenDesignLab(){if(phase!=Phase.MainMenu)return;phase=Phase.Lab;ResetLabDraft();RefreshLabWorkspace();labTreeScroll.verticalNormalizedPosition=1;labFieldsScroll.verticalNormalizedPosition=1;var group=labTree.Find("group-"+labGroup);if(group)RevealLabFocus(group.gameObject);RefreshInterface();Select(labBack);}
        void ResetLabDraft()
        {
            labBaseline=labHistory.Selected.Snapshot;labDraft=labBaseline.Clone();CacheLabIdentity();labRaw.Clear();labError=null;
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
            if(labDraft==null)return;var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;string oldFocus=selected?selected.name:null;float treePosition=labTreeScroll.verticalNormalizedPosition,fieldPosition=labFieldsScroll.verticalNormalizedPosition;
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
                (GroupLabel(LabGroup(d))+" "+d.Group+" "+d.Label+" "+d.Description+" "+d.Path).IndexOf(labQuery,StringComparison.OrdinalIgnoreCase)>=0:LabGroup(d)==labGroup).ToArray();
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
            Label(row.transform,"label",d.Label,18,new Vector2(.02f,.76f),new Vector2(.56f,.98f),TextAnchor.MiddleLeft,Color.white);
            Label(row.transform,"description",GroupLabel(LabGroup(d))+" · "+d.Description,14,new Vector2(.02f,.36f),new Vector2(.56f,.75f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            var input=LabInput(row.transform,"input-"+d.Path,labRaw.TryGetValue(d.Path,out var raw)?raw:LabNumber(labDraft.Get(d.Path)),new Vector2(.64f,.57f),new Vector2(.82f,.90f));
            var down=LabButton(row.transform,"minus-"+d.Path,"−",new Vector2(.58f,.57f),new Vector2(.63f,.90f),()=>EditLabValue(d,labDraft.Get(d.Path)-d.Step));
            var up=LabButton(row.transform,"plus-"+d.Path,"+",new Vector2(.83f,.57f),new Vector2(.88f,.90f),()=>EditLabValue(d,labDraft.Get(d.Path)+d.Step));
            var reset=LabButton(row.transform,"reset-"+d.Path,"Сброс",new Vector2(.89f,.57f),new Vector2(.98f,.90f),()=>{labRaw.Remove(d.Path);labDraft.Set(d.Path,labBaseline.Get(d.Path));RefreshLabValues();});
            Label(row.transform,"unit",d.Unit+" · "+LabNumber(d.Minimum)+"…"+LabNumber(d.Maximum)+" · шаг "+LabNumber(d.Step),14,new Vector2(.58f,.34f),new Vector2(.98f,.55f),TextAnchor.MiddleLeft,Color.white);
            var diff=Label(row.transform,"diff","",14,new Vector2(.02f,.17f),new Vector2(labAllChanges?.72f:.98f,.34f),TextAnchor.MiddleLeft,MenuGold);
            var error=Label(row.transform,"error","",14,new Vector2(.02f,.00f),new Vector2(labAllChanges?.72f:.98f,.17f),TextAnchor.MiddleLeft,new Color32(255,140,126,255));
            bool editable=!labHistory.SelectedProfileReadOnly&&labDraft.IsEditable(d.Path);input.interactable=editable;down.interactable=editable;up.interactable=editable;reset.interactable=editable;
            labViews[d.Path]=new LabFieldView{Input=input,Diff=diff,Error=error,Background=row.GetComponent<Image>()};
            input.onValueChanged.AddListener(value=>{if(labHistory.SelectedProfileReadOnly)return;labRaw[d.Path]=value;if(float.TryParse(value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out float n)&&!float.IsNaN(n)&&!float.IsInfinity(n)){labDraft.Set(d.Path,n);labRaw.Remove(d.Path);}RefreshLabValues(false);});
            string hint=(editable?labDraft.Domain(d.Path):"Только чтение · "+CombatBowlCatalog.Identity+" · новая геометрия требует отдельной map revision")+" · "+d.Description+"\n"+d.Path+" · "+d.Unit+" · диапазон "+d.Minimum+"…"+d.Maximum+" · шаг "+d.Step;
            foreach(var go in new[]{input.gameObject,down.gameObject,up.gameObject,reset.gameObject})Hint(go,hint);
            if(!editable){var info=LabButton(row.transform,"info-"+d.Path,"Описание",new Vector2(.80f,.02f),new Vector2(.98f,.20f),()=>labHint.text=hint);Hint(info.gameObject,hint);}
            if(labAllChanges){var jump=LabButton(row.transform,"jump-"+d.Path,"К полю",new Vector2(.75f,.035f),new Vector2(.98f,.32f),()=>{labAllChanges=false;labGroup=LabGroup(d);RefreshLabWorkspace();SelectLabControl(labViews[d.Path].Input);});Hint(jump.gameObject,"Открыть "+GroupLabel(LabGroup(d))+" и выбрать поле "+d.Label);}
        }
        static string LabNumber(float n)=>n.ToString("0.#####",CultureInfo.InvariantCulture);
        void EditLabValue(NumericDescriptor d,float n){if(labHistory.SelectedProfileReadOnly)return;labRaw.Remove(d.Path);labDraft.Set(d.Path,(float)Math.Round(n,6));RefreshLabValues();}
        List<ProfileValidationIssue> LabIssues()=>labDraft.Validate().Concat(labRaw.Keys.Select(p=>new ProfileValidationIssue{Path=p,Message="Введите конечное число"})).ToList();
        void RefreshLabValues(bool rewrite=true)
        {
            var issues=LabIssues();var baseline=labBaseline;
            foreach(var pair in labViews)
            {
                string path=pair.Key;var view=pair.Value;float old=baseline.Get(path),now=labDraft.Get(path);bool changed=labRaw.ContainsKey(path)||now!=old;
                if(rewrite)view.Input.SetTextWithoutNotify(labRaw.TryGetValue(path,out var raw)?raw:LabNumber(now));
                view.Diff.text=!labDraft.IsEditable(path)?"ТОЛЬКО ЧТЕНИЕ · "+CombatBowlCatalog.Identity+" · для изменения требуется новая map revision":changed?"ИЗМЕНЕНО · "+LabNumber(old)+" → "+(labRaw.TryGetValue(path,out var text)?text:LabNumber(now))+" · Δ "+(now-old>=0?"+":"")+LabNumber(now-old):"Без изменений";
                view.Error.text=string.Join("; ",issues.Where(e=>e.Path==path).Select(e=>e.Message));view.Background.color=changed?new Color32(42,63,61,255):MenuCard;
            }
            foreach(Transform row in labTree)
            {
                if(row.name.StartsWith("group-")){string group=row.name.Substring(6);int count=LabDescriptors().Count(d=>LabGroup(d)==group&&Changed(d));row.GetComponentInChildren<Text>().text="   "+GroupLabel(group).Split('→').Last().Trim()+(count>0?" · "+count:"");}
                else {string parent=row.name.Substring(7);row.GetComponent<Text>().text=parent+" · "+LabDescriptors().Count(d=>GroupLabel(LabGroup(d)).StartsWith(parent+" →",StringComparison.Ordinal)&&Changed(d));}
            }
            bool readOnly=labHistory.SelectedProfileReadOnly;labRename.interactable=!readOnly&&labHistory.Writable;labCreate.GetComponentInChildren<Text>().text=readOnly?"Создать копию":"Создать";
            labSave.interactable=!readOnly&&LabDirty&&issues.Count==0&&labHistory.Writable;labDelete.interactable=!labHistory.SelectedProfileProtected&&labHistory.Writable;
            var selected=labHistory.Selected;bool shipped=labHistory.IsShipped(selected);
            labRelease.interactable=!readOnly&&!LabDirty&&!shipped&&labHistory.Compatible(selected)&&labHistory.Writable;
            labRelease.GetComponentInChildren<Text>().text=shipped?"Релизная · в клиенте":selected.ReleaseCandidate?"Релизная: да · локально":"Релизная: нет";
            string name=labHistory.SelectedProfileName;labProfileSelector.GetComponentInChildren<Text>().text=(name.Length>24?name.Substring(0,21)+"…":name)+" ▾";labRevisionSelector.GetComponentInChildren<Text>().text=labSavedRevisionLabel+" ▾";
            labIdentity.text=labHistory.SelectedProfileProtected?"Стандартный профиль · удаление запрещено":"Локальный профиль";labStatus.text=labError??labHistory.StorageError??(readOnly?"Только чтение · для правок создайте копию":issues.Count>0?"Ошибок: "+issues.Count+" · сохранение заблокировано":LabDirty?"Черновик · правок "+LabDescriptors().Count(Changed):"Сохранённая ревизия · нет изменений");
        }
        IEnumerator SelectLabRevision(string profile,int revision)
        {
            if(labHistory.Busy)yield break;
            var card=LabDialog("Выбор ревизии…");
            var cancel=LabButton(card,"cancel","Назад",new Vector2(.70f,.10f),new Vector2(.95f,.185f),CloseLabDialog);
            cancel.interactable=false;
            Task operation=labHistory.SelectAsync(profile,revision);
            while(!operation.IsCompleted)yield return null;
            if(cancel){cancel.interactable=true;Select(cancel);}
            LabOperation(()=>operation.GetAwaiter().GetResult(),true);
        }
        void LabOperation(Action action,bool reset)
        {
            if(labHistory.Busy)return;
            try{action();if(reset)ResetLabDraft();else CacheLabIdentity();labError=null;CloseLabDialog();RefreshLabWorkspace();}
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
            if(labDialog!=null){labDialog.SetActive(false);Destroy(labDialog);labDialog=null;}
            foreach(var pair in labDialogControls)if(pair.Key)pair.Key.interactable=pair.Value;labDialogControls.Clear();
            if(EventSystem.current&&labDialogReturn&&labDialogReturn.activeInHierarchy)EventSystem.current.SetSelectedGameObject(labDialogReturn);else Select(labBack);labDialogReturn=null;if(labDraft!=null)RefreshLabValues();
        }
        Transform LabDialog(string title)
        {
            var previous=labDialog!=null?labDialogReturn:(EventSystem.current?EventSystem.current.currentSelectedGameObject:null);
            CloseLabDialog();labDialogReturn=previous;
            foreach(var control in labScreen.GetComponentsInChildren<Selectable>()){labDialogControls[control]=control.interactable;control.interactable=false;}
            labDialog=Panel(labScreen.transform,"lab-dialog",Vector2.zero,Vector2.one,new Color32(4,10,17,250));
            var card=Panel(labDialog.transform,"dialog-card",new Vector2(.22f,.27f),new Vector2(.78f,.73f),MenuCard);
            Label(card.transform,"title",title,20,new Vector2(.05f,.76f),new Vector2(.95f,.96f),TextAnchor.MiddleLeft,MenuGold);return card.transform;
        }
        void LabConfirm(string title,string confirm,Action action)
        {var card=LabDialog(title);var cancel=LabButton(card,"cancel","Продолжить редактирование",new Vector2(.05f,.10f),new Vector2(.55f,.185f),CloseLabDialog);LabButton(card,"confirm",confirm,new Vector2(.60f,.10f),new Vector2(.95f,.185f),()=>action());Select(cancel);}
        void ConfirmLabDiscard(Action next)=>LabConfirm("Черновик содержит несохранённые изменения","Отбросить",()=>{ResetLabDraft();CloseLabDialog();next();});
        void GuardLabSelection(Action action){if(LabDirty)ConfirmLabDiscard(action);else action();}
        void ShowLabErrors()
        {
            var issues=LabIssues();var card=LabDialog(issues.Count==0?"Нет ошибок":"Ошибки черновика");var scroll=LabScroll(card,"error-options",new Vector2(.05f,.17f),new Vector2(.95f,.75f),out var list);int i=0;
            foreach(var issue in issues){string path=issue.Path;var b=LabButton(list,"error-"+path,path+" · "+issue.Message,Vector2.zero,Vector2.one,()=>{CloseLabDialog();labQuery=path;labAllChanges=false;labSearch.SetTextWithoutNotify(path);RefreshLabWorkspace();if(labViews.TryGetValue(path,out var field))SelectLabControl(field.Input);});PlaceLabRow((RectTransform)b.transform,i++,50);b.GetComponentInChildren<Text>().fontSize=16;}
            ((RectTransform)list).sizeDelta=new Vector2(0,i*50);var cancel=LabButton(card,"cancel","Назад",new Vector2(.70f,.06f),new Vector2(.95f,.145f),CloseLabDialog);Select(cancel);
        }
        Transform LabDropdown(Button source)
        {
            var card=LabDialog("");labDialog.GetComponent<Image>().color=Color.clear;
            var r=(RectTransform)card;var button=(RectTransform)source.transform;
            r.anchorMin=new Vector2(button.anchorMin.x,button.anchorMin.y-.30f);r.anchorMax=new Vector2(Mathf.Min(.98f,button.anchorMin.x+.46f),button.anchorMin.y);r.offsetMin=r.offsetMax=Vector2.zero;
            card.Find("title").gameObject.SetActive(false);return card;
        }
        void ShowLabProfiles()
        {
            var card=LabDropdown(labProfileSelector);var scroll=LabScroll(card,"profile-options",new Vector2(.05f,.17f),new Vector2(.95f,.75f),out var list);int i=0;Button first=null;
            foreach(var profile in labHistory.Profiles){var p=profile;var b=LabButton(list,"profile-"+p.Id,p.Name,Vector2.zero,Vector2.one,()=>GuardLabSelection(()=>StartCoroutine(SelectLabRevision(p.Id,p.Revisions.Max(r=>r.Number)))));PlaceLabRow((RectTransform)b.transform,i++,LabOptionRowHeight);first=first??b;}
            ((RectTransform)list).sizeDelta=new Vector2(0,i*LabOptionRowHeight);LabButton(card,"cancel","Отмена",new Vector2(.70f,.06f),new Vector2(.95f,.145f),CloseLabDialog);if(first!=null)Select(first);
        }
        void ShowLabRevisions()
        {
            var card=LabDropdown(labRevisionSelector);var scroll=LabScroll(card,"revision-options",new Vector2(.05f,.17f),new Vector2(.95f,.75f),out var list);int i=0;Button first=null;
            var revisions=labHistory.SelectedProfile.Revisions;
            foreach(var r in revisions.Where(r=>labHistory.Compatible(r)||!labHistory.IsShipped(r)||!revisions.Any(other=>other.ReleaseSequence==r.ReleaseSequence&&labHistory.Compatible(other))).OrderByDescending(r=>r.Number)){int n=r.Number;var b=LabButton(list,"revision-"+n,r.Label+" · "+r.Hash.Substring(0,12)+(labHistory.IsShipped(r)?" · в клиенте":r.ReleaseCandidate?" · релизная (локально)":" · локальная")+(labHistory.Compatible(r)?"":" · прежняя схема"),Vector2.zero,Vector2.one,()=>GuardLabSelection(()=>StartCoroutine(SelectLabRevision(labHistory.SelectedProfileId,n))));b.interactable=labHistory.Compatible(r);PlaceLabRow((RectTransform)b.transform,i++,LabOptionRowHeight);if(b.interactable)first=first??b;}
            ((RectTransform)list).sizeDelta=new Vector2(0,i*LabOptionRowHeight);LabButton(card,"cancel","Отмена",new Vector2(.70f,.06f),new Vector2(.95f,.145f),CloseLabDialog);if(first!=null)Select(first);
        }
        void LabNameDialog(bool rename)
        {var card=LabDialog(rename?"Переименовать профиль":"Создать от выбранной сохранённой базы");var name=LabInput(card,"lab-profile-name",rename?labHistory.SelectedProfileName:labHistory.SelectedProfileReadOnly?labHistory.SelectedProfileName+" · копия":"Новый профиль",new Vector2(.05f,.52f),new Vector2(.95f,.605f));LabButton(card,"submit",rename?"Переименовать":"Создать",new Vector2(.60f,.15f),new Vector2(.95f,.235f),()=>{string value=name.text;if(rename)LabOperation(()=>labHistory.Rename(value),false);else GuardLabSelection(()=>LabOperation(()=>labHistory.Create(value),true));});LabButton(card,"cancel","Отмена",new Vector2(.05f,.15f),new Vector2(.55f,.235f),CloseLabDialog);SelectLabControl(name);}
        void UpdateDesignLab()
        {
            if(phase!=Phase.Lab||labHistory.Busy)return;bool back=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach(var pad in Gamepad.all)back|=pad.buttonEast.wasPressedThisFrame;
            if(back){if(labDialog!=null)CloseLabDialog();else ToMainMenu();}
        }
    }
}

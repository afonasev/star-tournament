using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        enum InlinePage { Closed, Fields, Identity, Screen, Team, Color, Create }
        sealed class InlineEditor
        {
            public InputDevice Device;
            public bool Russian=true,Upper=true;
            public string Target, Draft="";
            public InlinePage Page;
            public Button Focus;
            public string FocusKey;
            public readonly Dictionary<Button,string> Keys=new Dictionary<Button,string>();
            public GameObject LastSharedSelection;
            public readonly List<Button> Choices=new List<Button>();
            public GameObject Root;
            public ScrollRect Scroll;
            public Vector2 Direction;
            public float RepeatAt,Hold;
            public bool RemovalLatched,WestPending;
            public int OpenedFrame=-1;
            public string HoldTarget;
        }
        readonly Dictionary<int,InlineEditor> inlineEditors=new Dictionary<int,InlineEditor>();
        readonly Button[] inlineRemove=new Button[8];
        readonly LobbyRemovalRing[] inlineRings=new LobbyRemovalRing[8];
        InputDevice inlineInvokingDevice;
        bool inlineRouting,inlineKeyboardRouting;
        int inlinePointerParticipant=-1,inlineDeviceSignature;
        bool InlineAny=>inlineEditors.Values.Any(e=>e.Page!=InlinePage.Closed);
        int InlineParticipant(InlineEditor e)=>Enumerable.Range(0,RosterTotal).Where(p=>LobbyColorIdentity(p)==e.Target).DefaultIfEmpty(-1).First();
        InlineEditor InlineFor(InputDevice device)
        {
            device=device??(InputDevice)Keyboard.current??Gamepad.all.FirstOrDefault();
            if(device==null)return null;
            if(!inlineEditors.TryGetValue(device.deviceId,out var e))inlineEditors[device.deviceId]=e=new InlineEditor{Device=device};
            return e;
        }
        // The shared UI module keeps pointer/keyboard input. Each gamepad has its own
        // navigation/submit state while the lobby is visible; no last-device arbitration.
        void RouteInlineInput()
        {
            bool active=phase==Phase.Setup&&setupStep==2&&pendingSeat<0&&rosterEditing<0&&!(lobbyDialog&&lobbyDialog.activeSelf)&&!textEntryPanel;
            bool keyboardOwned=active&&inlineEditors.Values.Any(e=>e.Device is Keyboard&&e.Page!=InlinePage.Closed);
            int signature=MenuUiDevice?.deviceId??17;foreach(var device in InputSystem.devices.Where(d=>!(d is Gamepad)))signature=unchecked(signature*31+device.deviceId);
            if(inlineRouting==active&&inlineKeyboardRouting==keyboardOwned&&signature==inlineDeviceSignature)return;
            inlineDeviceSignature=signature;
            inlineRouting=active;inlineKeyboardRouting=keyboardOwned;
            if(active)menuInputModule.actionsAsset.devices=InputSystem.devices.Where(d=>!(d is Gamepad)&&IsMenuInputDevice(d)&&(!keyboardOwned||!(d is Keyboard))).ToArray();
            // A conditional array/null expression lifts null to an empty ReadOnlyArray,
            // which masks every device. Assign nullable null directly when restoring.
            else if(MenuUiDevice!=null)menuInputModule.actionsAsset.devices=InputSystem.devices.Where(IsMenuInputDevice).ToArray();
            else menuInputModule.actionsAsset.devices=null;
        }
        void OpenInlineEditor(int p)
        {
            if(phase!=Phase.Setup||p<0||p>=RosterTotal)return;
            var e=InlineFor(inlineInvokingDevice??(inlinePointerParticipant==p?(InputDevice)Keyboard.current:menuDevice));inlinePointerParticipant=-1;if(e==null)return;
            if(!IsPrimaryMenuDevice(e.Device)&&(p>=LocalSeatCount||input.DeviceAt(p)!=e.Device))return;
            string target=LobbyColorIdentity(p);
            if(inlineEditors.Values.Any(other=>other!=e&&other.Page!=InlinePage.Closed&&other.Target==target))return;
            CloseInlineEditor(e);e.Target=target;e.Page=InlinePage.Fields;e.OpenedFrame=Time.frameCount;e.Hold=0;e.Direction=Vector2.zero;
            BuildInlineEditor(e);RefreshInterface();RouteInlineInput();
        }
        void CloseInlineEditor(InlineEditor e)
        {
            if(e.Root){e.Root.SetActive(false);Destroy(e.Root);}e.Root=null;e.Scroll=null;e.Choices.Clear();e.Keys.Clear();e.FocusKey=null;
            e.Page=InlinePage.Closed;e.Hold=0;e.HoldTarget=null;e.WestPending=false;
        }
        void BackInlineEditor(InlineEditor e)
        {
            if(e.Page==InlinePage.Fields)
            {
                int p=InlineParticipant(e);CloseInlineEditor(e);e.Focus=p>=0?rosterCards[p]:lobbyMap;RefreshInterface();
                // Refresh can replace the destroyed field's shared selection. It must
                // not overwrite this device's surviving participant cursor next frame.
                e.LastSharedSelection=EventSystem.current?.currentSelectedGameObject;
            }
            else
            {
                var previous=e.Page;e.Page=previous==InlinePage.Create?InlinePage.Identity:InlinePage.Fields;e.Focus=null;
                e.FocusKey=previous==InlinePage.Create?"inline-create":previous==InlinePage.Screen?"inline-screen":previous==InlinePage.Team?"inline-team":"inline-identity";
                BuildInlineEditor(e);
            }
        }
        Button InlineButton(InlineEditor e,Transform parent,string id,string title,Vector2 min,Vector2 max,Action action,string semanticKey=null)
        {
            if(semanticKey==null){semanticKey=id;string marker="-"+InlineParticipant(e);int at=id.IndexOf(marker,StringComparison.Ordinal);if(at>=0)semanticKey=id.Remove(at,marker.Length);}
            Button b=null;b=RosterButton(parent,id,title,min,max,()=>{if(InlineParticipant(e)<0)return;e.Focus=b;e.FocusKey=semanticKey;action();});e.Keys[b]=semanticKey;
            b.GetComponent<MenuPresentation>().Compact=false;((RectTransform)b.transform).offsetMin=((RectTransform)b.transform).offsetMax=Vector2.zero;
            var t=b.GetComponentInChildren<Text>();t.rectTransform.offsetMin=new Vector2(3,1);t.rectTransform.offsetMax=new Vector2(-3,-1);t.fontSize=19;t.fontStyle=FontStyle.Normal;t.resizeTextForBestFit=true;t.resizeTextMinSize=15;t.resizeTextMaxSize=19;
            var hint=b.GetComponent<MenuActionHint>();hint.HideFocusedConfirm=true;hint.Shortcut=null;
            hint.Initialize(b,()=>e.Device,()=>false,()=>e.Focus==b);
            b.navigation=new Navigation{mode=Navigation.Mode.None};e.Choices.Add(b);
            return b;
        }
        void InlinePageTo(InlineEditor e,InlinePage page){e.Page=page;e.Focus=null;e.FocusKey=null;BuildInlineEditor(e);}
        void InlineCommit(InlineEditor e,Action<int> commit)
        {
            int p=InlineParticipant(e);if(p<0){CloseInlineEditor(e);return;}
            commit(p);rosterEditing=-1;e.Page=InlinePage.Fields;e.Focus=null;e.FocusKey=null;RefreshInterface();RebuildInlineEditors();
        }
        void BuildInlineEditor(InlineEditor e)
        {
            int p=InlineParticipant(e);if(p<0){CloseInlineEditor(e);return;}
            string previousKey=e.Focus&&e.Keys.TryGetValue(e.Focus,out var key)?key:e.FocusKey;
            float previousScroll=e.Scroll?e.Scroll.verticalNormalizedPosition:1;
            if(e.Root){e.Root.SetActive(false);Destroy(e.Root);}e.Choices.Clear();e.Keys.Clear();e.Scroll=null;
            e.Root=Panel(rosterCards[p].transform,"inline-editor-"+e.Device.deviceId,new Vector2(.025f,.035f),new Vector2(.975f,.76f),Color.clear);
            e.Root.GetComponent<Image>().raycastTarget=false;
            bool bot=RosterIsBot(p);
            if(e.Page==InlinePage.Fields)
            {
                InlineButton(e,e.Root.transform,"inline-identity-"+p,(bot?"Сложность: "+RosterDifficultyNames[RosterDifficultyAt(p)]:"Профиль: "+(identities.ProfileId(p)==null?"Без профиля":IdentityLabel(p)))+"  ›",new Vector2(0,.69f),new Vector2(1,.96f),()=>InlinePageTo(e,InlinePage.Identity));
                InlineButton(e,e.Root.transform,"inline-screen-"+p,"Экран: "+(p<LocalSeatCount?(LobbyPosition(p,LocalSeatCount)+1).ToString():"Без экрана")+"  ›",new Vector2(0,.36f),new Vector2(1,.63f),()=>InlinePageTo(e,InlinePage.Screen));
                if(SetupMode==NativeMatchMode.Teams)
                    InlineButton(e,e.Root.transform,"inline-team-"+p,"Команда: "+RosterTeamName(RosterTeamAt(p))+"  ›",new Vector2(0,.03f),new Vector2(1,.30f),()=>InlinePageTo(e,InlinePage.Team));
                else BuildInlineColors(e,p,e.Root.transform,.03f,.29f);
            }
            else if(e.Page==InlinePage.Identity)
            {
                if(bot)
                {
                    for(int i=0;i<3;i++){int d=i;InlineButton(e,e.Root.transform,"inline-difficulty-"+p+"-"+i,RosterDifficultyNames[i],new Vector2(0,.68f-i*.32f),new Vector2(1,.96f-i*.32f),()=>InlineCommit(e,n=>{if(n<LocalSeatCount)SetSeatDifficulty(n,d);else SetBotDifficulty(n-LocalSeatCount,d);}));}
                }
                else
                {
                    // Five rows plus a pinned creation action are the approved inline
                    // layout invariant. All additional entries remain in this scroll view.
                    var content=RosterScroll(e.Root.transform,"inline-profiles-"+p,new Vector2(0,.18f),Vector2.one,out var scroll);e.Scroll=scroll;
                    float rowHeight=((RectTransform)scroll.viewport).rect.height/5;
                    if(rowHeight<=0)rowHeight=27;
                    var profiles=playerProfiles.ByUsage().ToArray();
                    for(int i=0;i<=profiles.Length;i++)
                    {
                        string id=i==0?null:profiles[i-1].Id;string caption=i==0?"Без профиля":profiles[i-1].Name;
                        var b=InlineButton(e,content,"inline-profile-"+p+"-"+i,caption,Vector2.zero,Vector2.one,()=>InlineCommit(e,n=>{
                            if(id!=null&&(playerProfiles.Find(id)==null||identities.InUse(id,n)))return;
                            if(id==null)identities.ChooseGuest(n,input.DeviceAt(n)?.deviceId??(-100-n),MouseSensitivityPreference.Resolve(Profile),fps.Visible);else ChooseRosterProfile(n,id);
                        }),"profile:"+(id??"none"));
                        b.interactable=id==null||!identities.InUse(id,p);
                        var r=(RectTransform)b.transform;r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.offsetMin=new Vector2(3,-(i+1)*rowHeight+1);r.offsetMax=new Vector2(-8,-i*rowHeight-1);
                    }
                    ((RectTransform)content).sizeDelta=new Vector2(0,(profiles.Length+1)*rowHeight);
                    InlineButton(e,e.Root.transform,"inline-create-"+p,"＋ Создать профиль",Vector2.zero,new Vector2(1,.16f),()=>{e.Draft="";InlinePageTo(e,InlinePage.Create);});
                }
            }
            else if(e.Page==InlinePage.Screen)BuildInlineScreens(e,p);
            else if(e.Page==InlinePage.Team)
            {
                for(int i=0;i<2;i++){var team=i==0?NativeTeam.TeamA:NativeTeam.TeamB;InlineButton(e,e.Root.transform,"inline-team-choice-"+p+"-"+i,RosterTeamName(team),new Vector2(0,.52f-i*.48f),new Vector2(1,.94f-i*.48f),()=>InlineCommit(e,n=>{if(n<LocalSeatCount)SetTeam(n,team);else SetBotTeam(n-LocalSeatCount,team);}));}
            }
            else if(e.Page==InlinePage.Create)BuildInlineName(e,p);
            // Reordering or another participant claiming this item must never change
            // the meaning of an unconfirmed selection. Keep disabled focus as a no-op.
            e.Focus=previousKey==null?e.Choices.FirstOrDefault(b=>b.interactable):e.Choices.FirstOrDefault(b=>e.Keys[b]==previousKey);
            e.FocusKey=previousKey??(e.Focus?e.Keys[e.Focus]:null);
            if(e.Scroll)e.Scroll.verticalNormalizedPosition=previousScroll;
            PaintInlineFocus();
        }
        void BuildInlineColors(InlineEditor e,int p,Transform parent,float lo,float hi)
        {
            EnsureLobbyColors();var palette=NativeParticipantColors.Read(ParticipantPaletteProfile);
            for(int i=0;i<8;i++)
            {
                int color=i;var b=InlineButton(e,parent,"inline-color-"+p+"-"+i,lobbyColors[e.Target]==i?"✓":"",new Vector2(i/8f,lo),new Vector2((i+1)/8f-.012f,hi),()=>{SwapInlineColor(e.Target,color);RefreshInterface();BuildInlineEditor(e);});
                b.GetComponent<MenuPresentation>().SurfaceTint=palette[i];
                var r=(RectTransform)b.transform;float side=Mathf.Min(r.rect.width,r.rect.height);r.anchorMin=r.anchorMax=new Vector2((i+.5f)/8f,(lo+hi)*.5f);r.sizeDelta=new Vector2(side,side);r.anchoredPosition=Vector2.zero;
            }
        }
        void SwapInlineColor(string target,int color)
        {
            if(SetupMode!=NativeMatchMode.Ffa||color<0||color>=8)return;
            EnsureLobbyColors();if(!lobbyColors.TryGetValue(target,out int previous))return;
            string owner=lobbyColors.FirstOrDefault(kv=>kv.Key!=target&&kv.Value==color).Key;
            if(owner!=null)lobbyColors[owner]=previous;
            lobbyColors[target]=color;
        }
        void BuildInlineScreens(InlineEditor e,int p)
        {
            bool extra=p>=LocalSeatCount;int count=extra&&LocalSeatCount<4?LocalSeatCount+1:LocalSeatCount;
            for(int pos=0;pos<count;pos++)
            {
                int position=pos;int occupant=Enumerable.Range(0,LocalSeatCount).Where(s=>LobbyPosition(s,LocalSeatCount)==position).DefaultIfEmpty(-1).First();
                var v=LocalSeatLayout.Viewport(count,pos);
                var b=InlineButton(e,e.Root.transform,"inline-screen-choice-"+p+"-"+pos,(pos+1)+" · "+(occupant<0?"Свободно":RosterIsBot(occupant)?"Бот":IdentityLabel(occupant)),new Vector2(v.xMin,.23f+v.yMin*.74f),new Vector2(v.xMax-.015f,.23f+v.yMax*.74f-.025f),()=>InlineCommit(e,n=>AssignLobbyScreen(n,position)));
                b.interactable=!extra||occupant<0||RosterIsBot(occupant)||LocalSeatCount<4;
            }
            var standings=LocalSeatLayout.PersistentStandings(count);if(standings.HasValue)
            {var v=standings.Value;Label(e.Root.transform,"inline-screen-standings-"+p,"ТАБЛИЦА СЧЁТА",16,new Vector2(v.xMin,.23f+v.yMin*.74f),new Vector2(v.xMax,.23f+v.yMax*.74f),TextAnchor.MiddleCenter,RosterMuted);}
            if(RosterIsBot(p))
            {var off=InlineButton(e,e.Root.transform,"inline-screen-off-"+p,"Без экрана",Vector2.zero,new Vector2(1,.20f),()=>InlineCommit(e,n=>{rosterEditing=n;SetRosterBotView(false);}));off.interactable=extra||LocalSeatCount>1;}
        }
        void BuildInlineName(InlineEditor e,int p)
        {
            var value=Label(e.Root.transform,"inline-name-"+p,e.Draft.Length>0?e.Draft:"Имя профиля",19,new Vector2(0,.82f),Vector2.one,TextAnchor.MiddleLeft,Color.white);value.supportRichText=false;
            if(e.Device is Keyboard)
            {
                var field=value.gameObject.AddComponent<InputField>();field.textComponent=value;field.characterLimit=24;field.text=e.Draft;field.onValueChanged.AddListener(text=>e.Draft=text);
                value.raycastTarget=true;
            }
            string letters=e.Russian?"АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ":"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            if(!e.Upper)letters=letters.ToLowerInvariant();
            for(int i=0;i<letters.Length;i++)
            {char ch=letters[i];int row=i/12,col=i%12;InlineButton(e,e.Root.transform,"inline-key-"+p+"-"+i,ch.ToString(),new Vector2(col/12f,.65f-row*.145f),new Vector2((col+1)/12f-.006f,.79f-row*.145f),()=>{if(e.Draft.Length<24)e.Draft+=ch;value.text=e.Draft;});}
            InlineButton(e,e.Root.transform,"inline-language-"+p,"RU / EN",new Vector2(0,.18f),new Vector2(.49f,.32f),()=>{e.Russian=!e.Russian;BuildInlineEditor(e);});
            InlineButton(e,e.Root.transform,"inline-case-"+p,"А / а",new Vector2(.51f,.18f),new Vector2(1,.32f),()=>{e.Upper=!e.Upper;BuildInlineEditor(e);});
            InlineButton(e,e.Root.transform,"inline-space-"+p,"Пробел",new Vector2(0,0),new Vector2(.31f,.17f),()=>{if(e.Draft.Length<24)e.Draft+=" ";value.text=e.Draft;});
            InlineButton(e,e.Root.transform,"inline-delete-"+p,"⌫",new Vector2(.33f,0),new Vector2(.49f,.17f),()=>{if(e.Draft.Length>0)e.Draft=e.Draft.Substring(0,e.Draft.Length-1);value.text=e.Draft;});
            InlineButton(e,e.Root.transform,"inline-save-"+p,"Создать",new Vector2(.51f,0),new Vector2(1,.17f),()=>{if(string.IsNullOrWhiteSpace(e.Draft))return;CreateProfileWithGeneralSettings(e.Draft);InlinePageTo(e,InlinePage.Identity);RebuildInlineEditors();});
        }
        bool CanInlineRemove(int p)=>p>=0&&p<RosterTotal&&RosterTotal>1&&(p>=LocalSeatCount||LocalSeatCount>1||botSetup.Count>0);
        void RemoveInlineParticipant(int p)
        {
            if(!CanInlineRemove(p))return;
            string target=LobbyColorIdentity(p);
            foreach(var e in inlineEditors.Values.Where(e=>e.Target==target).ToArray())CloseInlineEditor(e);
            rosterEditing=p;rosterRemove.interactable=true;RemoveRosterParticipant();rosterEditing=-1;
            RebuildInlineEditors();
        }
        void RebuildInlineEditors()
        {foreach(var e in inlineEditors.Values.Where(e=>e.Page!=InlinePage.Closed).ToArray())BuildInlineEditor(e);}
        void RefreshInlineCards()
        {
            for(int p=0;p<RosterTotal;p++)
            {
                int participant=p;string target=LobbyColorIdentity(p);bool editing=inlineEditors.Values.Any(e=>e.Target==target&&e.Page!=InlinePage.Closed);
                var card=rosterCards[p];
                if(!card.GetComponent<InlineRosterPointer>())card.gameObject.AddComponent<InlineRosterPointer>().Down=()=>inlinePointerParticipant=participant;
                var cardHint=card.GetComponent<MenuActionHint>();cardHint.HideFocusedConfirm=true;cardHint.enabled=false;cardHint.GlyphLabel.enabled=false;
                foreach(string hintName in new[]{"gamepad-action-button","gamepad-shortcut-button"}){var hintNode=card.transform.Find(hintName);if(hintNode)hintNode.GetComponent<Image>().enabled=false;}
                if(!inlineRemove[p])
                {
                    var b=MenuButton(card.transform,"inline-remove-"+p,"×",new Vector2(.90f,.80f),new Vector2(.995f,.995f),()=>RemoveInlineParticipant(participant));inlineRemove[p]=b;
                    b.GetComponent<MenuActionHint>().Initialize(b,()=>null,()=>false);b.GetComponent<MenuPresentation>().enabled=false;
                    b.GetComponent<Image>().enabled=false;b.transition=Selectable.Transition.None;
                    var label=b.GetComponentInChildren<Text>();label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;label.fontSize=28;label.alignment=TextAnchor.MiddleCenter;
                    var hit=Panel(b.transform,"remove-hit",Vector2.zero,Vector2.one,Color.clear).GetComponent<Image>();hit.raycastTarget=true;hit.transform.SetAsFirstSibling();
                    var ring=new GameObject("remove-progress",typeof(RectTransform),typeof(CanvasRenderer),typeof(LobbyRemovalRing));ring.transform.SetParent(b.transform,false);Layout((RectTransform)ring.transform,Vector2.zero,Vector2.one);inlineRings[p]=ring.GetComponent<LobbyRemovalRing>();inlineRings[p].color=MenuGold;inlineRings[p].raycastTarget=false;
                }
                inlineRemove[p].interactable=CanInlineRemove(p);inlineRemove[p].transform.SetAsLastSibling();
                if(editing)
                {
                    Layout(rosterPortraits[p].rectTransform,new Vector2(.02f,.775f),new Vector2(.17f,.99f));
                    Layout(rosterNames[p].rectTransform,new Vector2(.19f,.78f),new Vector2(.88f,.99f));rosterNames[p].fontSize=24;rosterNames[p].resizeTextMaxSize=24;
                    rosterDetails[p].gameObject.SetActive(false);rosterScreenIcons[p].SetActive(false);
                }
                else
                {
                    Layout(rosterPortraits[p].rectTransform,new Vector2(.025f,.04f),new Vector2(.49f,.96f));Layout(rosterNames[p].rectTransform,new Vector2(.52f,.69f),new Vector2(.89f,.94f));rosterNames[p].resizeTextMaxSize=30;
                }
                var name=rosterNames[p];name.alignment=TextAnchor.MiddleLeft;name.resizeTextForBestFit=false;name.fontSize=editing?24:28;
                var nameRect=name.rectTransform;float room=Mathf.Max(1,nameRect.rect.width-44);
                // Fit the caption before placing its adjacent device icon. Automatic
                // best-fit font generation otherwise differs from preferredWidth.
                while(name.fontSize>20&&name.preferredWidth>room)name.fontSize--;
                string full=name.text;
                while(name.preferredWidth>room&&full.Length>1){full=full.Substring(0,full.Length-1);name.text=full+"…";}
                float end=name.preferredWidth+8;
                var icon=(RectTransform)rosterDeviceIcons[p].transform;icon.SetParent(card.transform,false);icon.anchorMin=icon.anchorMax=new Vector2(nameRect.anchorMin.x,(nameRect.anchorMin.y+nameRect.anchorMax.y)*.5f);icon.pivot=new Vector2(0,.5f);icon.sizeDelta=new Vector2(32,22);icon.anchoredPosition=new Vector2(end,0);
            }
            foreach(var e in inlineEditors.Values.Where(e=>e.Page==InlinePage.Fields))
                foreach(var b in e.Choices.Where(b=>b&&b.name.StartsWith("inline-color-")))
                    b.GetComponentInChildren<Text>().text=lobbyColors.TryGetValue(e.Target,out var color)&&b.name.EndsWith("-"+color)?"✓":"";
            PaintInlineFocus();RouteInlineInput();
        }
        bool InlineRepeat(InlineEditor e,Vector2 nav)
        {
            if(nav!=e.Direction){e.Direction=nav;e.RepeatAt=Time.unscaledTime+menuInputModule.moveRepeatDelay;return nav!=Vector2.zero;}
            if(nav==Vector2.zero||Time.unscaledTime<e.RepeatAt)return false;
            e.RepeatAt=Time.unscaledTime+menuInputModule.moveRepeatRate;return true;
        }
        void MoveInlineFocus(InlineEditor e,Vector2 nav)
        {
            if(e.Page==InlinePage.Closed)
            {
                if(!e.Focus||!e.Focus.gameObject.activeInHierarchy)e.Focus=lobbyMap;
                var n=e.Focus.navigation;var next=nav.y>0?n.selectOnUp:nav.y<0?n.selectOnDown:nav.x>0?n.selectOnRight:n.selectOnLeft;
                if(next&&next.IsInteractable())e.Focus=next as Button;
            }
            else
            {
                var choices=e.Choices.Where(b=>b&&b.interactable).ToArray();if(choices.Length==0)return;
                int current=Array.IndexOf(choices,e.Focus);
                if(e.Page==InlinePage.Identity)
                {int step=nav.y>0?-1:nav.y<0?1:0;e.Focus=choices[Mathf.Clamp(current+step,0,choices.Length-1)];}
                else
                {
                    Vector2 origin=e.Focus?e.Focus.transform.position:choices[0].transform.position;
                    var next=choices.Where(b=>b!=e.Focus).Select(b=>new{Button=b,Delta=(Vector2)b.transform.position-origin}).Where(x=>Vector2.Dot(x.Delta,nav)>1).OrderBy(x=>Mathf.Abs(Vector2.Dot(x.Delta,new Vector2(-nav.y,nav.x)))<1?0:1).ThenBy(x=>x.Delta.sqrMagnitude/Vector2.Dot(x.Delta,nav)).FirstOrDefault();
                    if(next!=null)e.Focus=next.Button;
                }
                if(e.Scroll&&e.Focus&&e.Focus.transform.IsChildOf(e.Scroll.content))
                {
                    int index=e.Choices.IndexOf(e.Focus);float rows=e.Choices.Count-1;
                    e.Scroll.verticalNormalizedPosition=rows<=5?1:1-Mathf.Clamp01((index-2)/(rows-5));
                }
            }
            gameAudio?.MenuMove();
        }
        void PaintInlineFocus()
        {
            foreach(var e in inlineEditors.Values)
                foreach(var b in e.Choices)if(b)b.GetComponent<MenuPresentation>().SetFocused(e.Focus==b);
            foreach(var b in new[]{lobbyMap,lobbyMode,lobbyTime,lobbyTarget,setupPrevious,rosterAddBot,rosterAddHuman,start}.Concat(rosterEmpty).Where(b=>b))
                b.GetComponent<MenuPresentation>().SetFocused(inlineEditors.Values.Any(e=>e.Page==InlinePage.Closed&&e.Focus==b)||(!menuGamepadCursor&&EventSystem.current&&EventSystem.current.currentSelectedGameObject==b.gameObject));
            for(int p=0;p<RosterTotal;p++)
            {
                bool focused=inlineEditors.Values.Any(e=>e.Page==InlinePage.Closed?e.Focus==rosterCards[p]:e.Target==LobbyColorIdentity(p));
                rosterCards[p].GetComponent<MenuPresentation>().SetFocused(focused||(!menuGamepadCursor&&EventSystem.current&&EventSystem.current.currentSelectedGameObject==rosterCards[p].gameObject));
                if(inlineRings[p]){inlineRings[p].Progress=inlineEditors.Values.Where(e=>e.HoldTarget==LobbyColorIdentity(p)).Select(e=>e.Hold/LobbyProfile.Get("lobby.removeHoldSeconds")).DefaultIfEmpty(0).Max();inlineRings[p].SetVerticesDirty();}
            }
        }
        bool UpdateInlineInput()
        {
            RouteInlineInput();if(menuBackConsumedFrame==Time.frameCount)return true;
            if(!inlineRouting||setupTransitionFrame==Time.frameCount)return false;
            if(!menuWindowFocused){foreach(var context in inlineEditors.Values){context.Hold=0;context.HoldTarget=null;context.WestPending=false;context.Direction=Vector2.zero;}PaintInlineFocus();return false;}
            foreach(var pad in Gamepad.all.Where(p=>p.added&&p.enabled))
            {
                var e=InlineFor(pad);if(e.OpenedFrame==Time.frameCount)continue;
                bool primary=IsPrimaryMenuDevice(pad);
                if(!primary&&e.Page==InlinePage.Closed){e.Focus=null;e.Hold=0;e.HoldTarget=null;e.WestPending=false;continue;}
                var shared=EventSystem.current?.currentSelectedGameObject;
                if(e.Page==InlinePage.Closed&&shared!=e.LastSharedSelection){e.Focus=shared?shared.GetComponent<Button>():lobbyMap;e.LastSharedSelection=shared;}
                if(e.Page==InlinePage.Closed&&(!e.Focus||!e.Focus.gameObject.activeInHierarchy))e.Focus=lobbyMap;
                if(e.Page!=InlinePage.Closed&&(InlineParticipant(e)<0||!primary&&(InlineParticipant(e)>=LocalSeatCount||input.DeviceAt(InlineParticipant(e))!=pad))){CloseInlineEditor(e);continue;}
                var nav=pad.dpad.ReadValue();if(nav==Vector2.zero)nav=pad.leftStick.ReadValue();nav=new Vector2(Mathf.Abs(nav.x)>.5f?Mathf.Sign(nav.x):0,Mathf.Abs(nav.y)>.5f?Mathf.Sign(nav.y):0);
                if(InlineRepeat(e,nav))MoveInlineFocus(e,nav);
                if(pad.buttonEast.wasPressedThisFrame)
                {
                    menuBackConsumedFrame=Time.frameCount;setupTransitionFrame=Time.frameCount;
                    if(e.Page!=InlinePage.Closed)BackInlineEditor(e);else if(primary&&!InlineAny)PreviousSetupStep();
                    continue;
                }
                int participant=e.Page==InlinePage.Closed?Array.IndexOf(rosterCards,e.Focus):InlineParticipant(e);
                if(participant>=RosterTotal)participant=-1;
                if(primary&&pad.buttonWest.wasPressedThisFrame&&e.Page==InlinePage.Closed)e.WestPending=true;
                if(!pad.buttonWest.isPressed)
                {
                    bool tap=primary&&e.WestPending&&!e.RemovalLatched;e.WestPending=false;e.RemovalLatched=false;
                    if(tap){e.Hold=0;e.HoldTarget=null;BeginLobbyBot();continue;}
                }
                if(primary&&pad.buttonWest.isPressed&&!e.RemovalLatched&&participant>=0)
                {
                    string target=LobbyColorIdentity(participant);if(e.HoldTarget!=target){e.HoldTarget=target;e.Hold=0;}
                    e.Hold+=Time.unscaledDeltaTime;
                    if(e.Hold>=LobbyProfile.Get("lobby.removeHoldSeconds"))
                    {
                        RemoveInlineParticipant(participant);e.Hold=0;e.HoldTarget=null;e.RemovalLatched=true;
                        // Preserve the nearest surviving card while requiring a release
                        // before another removal can begin on its new identity.
                        e.Focus=RosterTotal>0?rosterCards[Mathf.Clamp(participant,0,RosterTotal-1)]:lobbyMap;
                        e.LastSharedSelection=EventSystem.current?.currentSelectedGameObject;
                    }
                }
                else {e.Hold=0;e.HoldTarget=null;}
                if(pad.buttonSouth.wasPressedThisFrame&&e.Focus&&e.Focus.IsInteractable())
                {
                    inlineInvokingDevice=pad;e.Focus.onClick.Invoke();inlineInvokingDevice=null;
                }
            }
            var disconnected=inlineEditors.Values.Where(e=>(e.Page!=InlinePage.Closed||e.Hold>0||e.WestPending)&&(!e.Device.added||!e.Device.enabled)).ToArray();
            foreach(var e in disconnected)CloseInlineEditor(e);
            if(disconnected.Length>0)RefreshInterface();
            var keyboard=Keyboard.current;
            if(keyboard!=null&&keyboard.added&&keyboard.enabled)
            {
                var e=InlineFor(keyboard);
                if(e!=null&&e.Page!=InlinePage.Closed&&e.OpenedFrame!=Time.frameCount)
                {
                    var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                    var field=selected?selected.GetComponent<InputField>():null;
                    bool typing=field&&field.isFocused;
                    if(keyboard.escapeKey.wasPressedThisFrame)
                    {if(typing)field.DeactivateInputField();else BackInlineEditor(e);menuBackConsumedFrame=Time.frameCount;}
                    else if(!typing)
                    {
                        var nav=new Vector2((keyboard.rightArrowKey.isPressed?1:0)-(keyboard.leftArrowKey.isPressed?1:0),(keyboard.upArrowKey.isPressed?1:0)-(keyboard.downArrowKey.isPressed?1:0));
                        if(InlineRepeat(e,nav))MoveInlineFocus(e,nav);
                        if(keyboard.enterKey.wasPressedThisFrame&&e.Focus&&e.Focus.IsInteractable())e.Focus.onClick.Invoke();
                    }
                }
            }
            PaintInlineFocus();return menuBackConsumedFrame==Time.frameCount;
        }
        bool InlineWestOwns(Gamepad pad)
        {
            if(!inlineEditors.TryGetValue(pad.deviceId,out var e))return false;
            return !IsPrimaryMenuDevice(pad)||e.Page!=InlinePage.Closed||e.WestPending||Array.IndexOf(rosterCards,e.Focus)>=0||e.HoldTarget!=null;
        }
    }
    public sealed class InlineRosterPointer : MonoBehaviour,IPointerDownHandler
    {
        public Action Down;
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Down?.Invoke();}
    }
    // Procedural ring avoids a new texture or per-frame allocation; only its mesh changes.
    public sealed class LobbyRemovalRing : MaskableGraphic
    {
        public float Progress;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(Progress<=0)return;
            const int segments=48;var rect=rectTransform.rect;float radius=Mathf.Min(rect.width,rect.height)*.46f,thickness=2;
            int count=Mathf.CeilToInt(Mathf.Clamp01(Progress)*segments);
            for(int i=0;i<count;i++)
            {
                float a=Mathf.PI*.5f-i*Mathf.PI*2/segments,b=Mathf.PI*.5f-Mathf.Min(i+1,Progress*segments)*Mathf.PI*2/segments;
                Vector2 x=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),y=new Vector2(Mathf.Cos(b),Mathf.Sin(b));int n=vh.currentVertCount;
                vh.AddVert(rect.center+x*radius,color,Vector2.zero);vh.AddVert(rect.center+x*(radius-thickness),color,Vector2.zero);vh.AddVert(rect.center+y*(radius-thickness),color,Vector2.zero);vh.AddVert(rect.center+y*radius,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}

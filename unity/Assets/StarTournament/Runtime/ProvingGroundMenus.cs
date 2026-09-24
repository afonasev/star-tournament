using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        static readonly Color32 MenuInk = new Color32(17,30,41,255);
        static readonly Color32 MenuCard = new Color32(28,49,60,255);
        static readonly Color32 MenuGold = new Color32(255,189,98,255);
        GameObject oldMenuColumn, mainMenuScreen, profilesScreen, settingsScreen, labScreen, identityScreen;
        Transform profileList, identityList;
        Text profileDetail, identityHeading, settingsHeading, labDetail;
        InputField profileNameInput;
        Button mainBattleButton, profilesBackButton;
        Button identitySettingsButton;
        Button[] seatIdentityButtons = new Button[SeatInputCoordinator.SeatCount];
        string selectedProfileId;
        int pendingSeat = -1, settingsSeat = -1, labIndex;
        Phase settingsReturn = Phase.MainMenu;

        GameObject Panel(Transform parent,string name,Vector2 min,Vector2 max,Color color)
        {
            var panel=new GameObject(name,typeof(RectTransform),typeof(Image));panel.transform.SetParent(parent,false);
            Layout((RectTransform)panel.transform,min,max);panel.GetComponent<Image>().color=color;
            return panel;
        }
        Text Label(Transform parent,string name,string value,int size,Vector2 min,Vector2 max,TextAnchor align,Color color)
        {
            var text=TextElement(parent,name,value,size);Layout(text.rectTransform,min,max);text.alignment=align;text.color=color;return text;
        }
        Button MenuButton(Transform parent,string name,string value,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action,bool enabled=true)
        {
            var button=ButtonElement(parent,value,action);button.name=name;Layout((RectTransform)button.transform,min,max);
            var image=button.GetComponent<Image>();image.color=MenuCard;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=MenuGold;
            colors.selectedColor=MenuGold;colors.pressedColor=new Color(.75f,.57f,.32f);colors.disabledColor=new Color(.43f,.50f,.52f);
            button.colors=colors;button.interactable=enabled;
            return button;
        }
        void CreateModernMenuUi(Transform parent)
        {
            oldMenuColumn=parent.Find("menu").gameObject;
            mainMenuScreen=Panel(parent,"main-menu",Vector2.zero,Vector2.one,MenuInk);
            var left=Panel(mainMenuScreen.transform,"actions",new Vector2(.06f,.13f),new Vector2(.53f,.87f),new Color32(20,39,53,255));
            var right=Panel(mainMenuScreen.transform,"broadcast",new Vector2(.56f,.13f),new Vector2(.94f,.87f),MenuCard);
            Label(left.transform,"brand","STAR TOURNAMENT",38,new Vector2(.07f,.85f),new Vector2(.95f,.97f),TextAnchor.MiddleLeft,MenuGold);
            string[] labels={"Битва","Сетевая игра · скоро","Лаборатория геймдизайна","Профили игроков","Настройки игры","Выход"};
            UnityEngine.Events.UnityAction[] actions={OpenSetup,()=>{},OpenLab,OpenProfiles,()=>OpenSettings(-1),()=>Application.Quit()};
            for(int i=0;i<labels.Length;i++)
            {
                float top=.79f-i*.115f;
                var button=MenuButton(left.transform,"main-action-"+i,labels[i],new Vector2(.07f,top-.095f),new Vector2(.93f,top),actions[i],i!=1);
                if(i==0)mainBattleButton=button;
            }
            Label(right.transform,"bowl","КОМБАТ БОУЛ",42,new Vector2(.08f,.73f),new Vector2(.93f,.88f),TextAnchor.MiddleLeft,Color.white);
            Label(right.transform,"league","ОРБИТАЛЬНАЯ ЛИГА",26,new Vector2(.08f,.65f),new Vector2(.93f,.74f),TextAnchor.MiddleLeft,MenuGold);
            Label(right.transform,"route","01 / ЛОКАЛЬНЫЙ МАТЧ\n\nВыберите состав, профили и устройства",26,new Vector2(.08f,.36f),new Vector2(.93f,.61f),TextAnchor.UpperLeft,Color.white);
            Label(right.transform,"controls","МЫШЬ: выбор   ·   ГЕЙМПАД: крестовина / A",22,new Vector2(.08f,.07f),new Vector2(.95f,.19f),TextAnchor.MiddleLeft,new Color32(177,199,201,255));

            profilesScreen=Panel(parent,"profiles-screen",Vector2.zero,Vector2.one,MenuInk);
            var listCard=Panel(profilesScreen.transform,"profile-list-card",new Vector2(.06f,.12f),new Vector2(.57f,.89f),new Color32(20,39,53,255));
            var detailCard=Panel(profilesScreen.transform,"profile-detail-card",new Vector2(.59f,.12f),new Vector2(.94f,.89f),MenuCard);
            Label(listCard.transform,"profiles-title","ПРОФИЛИ ИГРОКОВ",35,new Vector2(.06f,.86f),new Vector2(.94f,.98f),TextAnchor.MiddleLeft,Color.white);
            var list=Panel(listCard.transform,"profile-list",new Vector2(.06f,.20f),new Vector2(.94f,.84f),new Color32(20,39,53,255));profileList=list.transform;
            MenuButton(listCard.transform,"profile-create","+ СОЗДАТЬ ПРОФИЛЬ",new Vector2(.06f,.10f),new Vector2(.94f,.18f),CreateNamedProfile);
            profilesBackButton=MenuButton(listCard.transform,"profiles-back","‹ НАЗАД",new Vector2(.06f,.01f),new Vector2(.94f,.08f),ToMainMenu);
            profileDetail=Label(detailCard.transform,"profile-detail","Выберите профиль",27,new Vector2(.08f,.77f),new Vector2(.95f,.97f),TextAnchor.MiddleLeft,MenuGold);
            profileNameInput=CreateNameInput(detailCard.transform);
            MenuButton(detailCard.transform,"profile-rename","Переименовать",new Vector2(.08f,.57f),new Vector2(.92f,.64f),RenameSelectedProfile);
            MenuButton(detailCard.transform,"profile-sensitivity-down","Чувствительность мыши −",new Vector2(.08f,.45f),new Vector2(.92f,.54f),()=>StepSelectedProfile(-1));
            MenuButton(detailCard.transform,"profile-sensitivity-up","Чувствительность мыши +",new Vector2(.08f,.34f),new Vector2(.92f,.43f),()=>StepSelectedProfile(1));
            MenuButton(detailCard.transform,"profile-fps","Показывать FPS · переключить",new Vector2(.08f,.23f),new Vector2(.92f,.32f),ToggleSelectedProfileFps);
            MenuButton(detailCard.transform,"profile-delete","Удалить профиль",new Vector2(.08f,.06f),new Vector2(.92f,.16f),DeleteSelectedProfile);

            settingsScreen=Panel(parent,"settings-screen",Vector2.zero,Vector2.one,MenuInk);
            var settingsCard=Panel(settingsScreen.transform,"settings-card",new Vector2(.21f,.16f),new Vector2(.79f,.84f),MenuCard);
            settingsHeading=Label(settingsCard.transform,"settings-heading","НАСТРОЙКИ ИГРЫ",37,new Vector2(.06f,.80f),new Vector2(.94f,.95f),TextAnchor.MiddleCenter,MenuGold);
            MenuButton(settingsCard.transform,"settings-sensitivity-minus","Чувствительность мыши −",new Vector2(.10f,.59f),new Vector2(.90f,.70f),()=>StepMenuSensitivity(-1));
            MenuButton(settingsCard.transform,"settings-sensitivity-plus","Чувствительность мыши +",new Vector2(.10f,.46f),new Vector2(.90f,.57f),()=>StepMenuSensitivity(1));
            MenuButton(settingsCard.transform,"settings-fps","Показывать FPS · переключить",new Vector2(.10f,.33f),new Vector2(.90f,.44f),ToggleMenuFps);
            MenuButton(settingsCard.transform,"settings-back","‹ НАЗАД",new Vector2(.10f,.12f),new Vector2(.90f,.23f),CloseSettings);

            labScreen=Panel(parent,"lab-screen",Vector2.zero,Vector2.one,MenuInk);
            var labCard=Panel(labScreen.transform,"lab-card",new Vector2(.20f,.15f),new Vector2(.80f,.85f),MenuCard);
            Label(labCard.transform,"lab-heading","ЛАБОРАТОРИЯ ГЕЙМДИЗАЙНА",34,new Vector2(.05f,.82f),new Vector2(.95f,.96f),TextAnchor.MiddleCenter,MenuGold);
            labDetail=Label(labCard.transform,"lab-detail","",26,new Vector2(.07f,.42f),new Vector2(.93f,.78f),TextAnchor.MiddleCenter,Color.white);
            MenuButton(labCard.transform,"lab-prev","‹ Параметр",new Vector2(.06f,.31f),new Vector2(.48f,.40f),()=>{labIndex=(labIndex+Profile.Descriptors.Count-1)%Profile.Descriptors.Count;RefreshLab();});
            MenuButton(labCard.transform,"lab-next","Параметр ›",new Vector2(.52f,.31f),new Vector2(.94f,.40f),()=>{labIndex=(labIndex+1)%Profile.Descriptors.Count;RefreshLab();});
            MenuButton(labCard.transform,"lab-down","−",new Vector2(.06f,.20f),new Vector2(.48f,.29f),()=>StepLab(-1));
            MenuButton(labCard.transform,"lab-up","+",new Vector2(.52f,.20f),new Vector2(.94f,.29f),()=>StepLab(1));
            MenuButton(labCard.transform,"lab-back","‹ ГЛАВНОЕ МЕНЮ",new Vector2(.06f,.05f),new Vector2(.94f,.15f),ToMainMenu);

            identityScreen=Panel(parent,"identity-picker",Vector2.zero,Vector2.one,new Color32(8,17,25,248));
            var identityCard=Panel(identityScreen.transform,"identity-card",new Vector2(.17f,.12f),new Vector2(.83f,.88f),new Color32(20,39,53,255));
            identityHeading=Label(identityCard.transform,"identity-heading","КТО ИГРАЕТ?",35,new Vector2(.06f,.85f),new Vector2(.94f,.97f),TextAnchor.MiddleLeft,MenuGold);
            var options=Panel(identityCard.transform,"identity-options",new Vector2(.06f,.19f),new Vector2(.94f,.82f),new Color32(20,39,53,255));identityList=options.transform;
            MenuButton(identityCard.transform,"identity-cancel","‹ НАЗАД",new Vector2(.06f,.06f),new Vector2(.48f,.16f),()=>{pendingSeat=-1;RefreshInterface();});
            identitySettingsButton=MenuButton(identityCard.transform,"identity-settings","Личные настройки",new Vector2(.52f,.06f),new Vector2(.94f,.16f),()=>{int seat=pendingSeat;pendingSeat=-1;OpenSettings(seat);});
            for(int seat=0;seat<seatIdentityButtons.Length;seat++)
            {
                int localSeat=seat;
                seatIdentityButtons[seat]=MenuButton(parent,"seat-identity-"+seat,"Выбрать профиль",new Vector2(.53f,.57f-seat*.07f),new Vector2(.97f,.63f-seat*.07f),()=>OpenIdentityPicker(localSeat));
            }
        }
        InputField CreateNameInput(Transform parent)
        {
            var shell=Panel(parent,"profile-name-input",new Vector2(.08f,.67f),new Vector2(.92f,.76f),new Color32(38,61,72,255));
            var input=shell.AddComponent<InputField>();
            var value=Label(shell.transform,"name-value","",26,new Vector2(.04f,0),new Vector2(.96f,1),TextAnchor.MiddleLeft,Color.white);
            input.textComponent=value;input.characterLimit=24;input.contentType=InputField.ContentType.Standard;
            return input;
        }
        void OpenSetup() { fps.RestoreGeneralPreference();phase=Phase.Setup;setupError=null;RefreshInterface();Select(rebind); }
        void ToMainMenu()
        {
            ClearSeatPauseMenus();
            if(phase==Phase.Running||phase==Phase.Paused||phase==Phase.Results)Menu();
            pendingSeat=-1;fps.RestoreGeneralPreference();phase=Phase.MainMenu;SetCursor(false);RefreshInterface();Select(mainBattleButton);
        }
        void OpenProfiles() { phase=Phase.Profiles;RefreshProfilesUi();RefreshInterface();Select(profilesBackButton); }
        void OpenLab() { phase=Phase.Lab;RefreshLab();RefreshInterface(); }
        void OpenSettings(int seat)
        {
            settingsReturn=phase;settingsSeat=seat;phase=Phase.Settings;RefreshInterface();
        }
        void CloseSettings() { phase=settingsReturn;settingsSeat=-1;RefreshInterface(); }
        void OnDeviceJoined(int seat,InputDevice device)
        {
            setupError=null;
            identities.ClearSeat(seat);
            if(!identities.TryRestore(seat,device.deviceId,playerProfiles))OpenIdentityPicker(seat);
            else RefreshInterface();
        }
        void OpenIdentityPicker(int seat)
        {
            if(phase!=Phase.Setup || !input.IsHumanSeat(seat))return;
            pendingSeat=seat;RefreshIdentityOptions();RefreshInterface();
        }
        void RefreshIdentityOptions()
        {
            if(identityList==null||pendingSeat<0)return;
            foreach(Transform child in identityList){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            identityHeading.text="БИТВА  /  ИГРОК "+(pendingSeat+1)+"  /  "+
                (input.IsConnected(pendingSeat)?input.Label(pendingSeat):"УСТРОЙСТВО ОТКЛЮЧЕНО");
            identitySettingsButton.interactable=identities.HasIdentity(pendingSeat);
            var profiles=playerProfiles.Profiles.ToArray();
            int rows=profiles.Length+2;
            for(int i=0;i<profiles.Length;i++)
            {
                var record=profiles[i];bool occupied=identities.InUse(record.Id,pendingSeat);
                float top=1f-i/(float)rows;
                MenuButton(identityList,"identity-profile-"+record.Id,record.Name+(occupied?" · Занят другим игроком":" · Свободен"),
                    new Vector2(0,top-1f/rows+.015f),new Vector2(1,top-.015f),()=>ChooseIdentity(record.Id),!occupied);
            }
            float createTop=1f-profiles.Length/(float)rows;
            MenuButton(identityList,"identity-create","+ Создать профиль",new Vector2(0,createTop-1f/rows+.015f),new Vector2(1,createTop-.015f),CreateAndChooseProfile);
            float guestTop=createTop-1f/rows;
            MenuButton(identityList,"identity-guest","Без профиля · гость до выхода",new Vector2(0,guestTop-1f/rows+.015f),new Vector2(1,guestTop-.015f),ChooseGuest);
        }
        void ChooseIdentity(string id)
        {
            var device=input.DeviceAt(pendingSeat);
            if(!input.IsConnected(pendingSeat)||device==null||!identities.ChooseProfile(pendingSeat,device.deviceId,id,playerProfiles))
            {setupError="Профиль занят или устройство отключено";RefreshIdentityOptions();return;}
            pendingSeat=-1;RefreshInterface();Select(start);
        }
        void CreateAndChooseProfile()
        {
            var profile=playerProfiles.Create("Игрок "+(playerProfiles.Profiles.Count+1),MouseSensitivityPreference.Resolve(Profile),fps.Visible);
            ChooseIdentity(profile.Id);
        }
        void ChooseGuest()
        {
            var device=input.DeviceAt(pendingSeat);
            if(!input.IsConnected(pendingSeat)||device==null){setupError="Устройство отключено";RefreshInterface();return;}
            identities.ChooseGuest(pendingSeat,device.deviceId,MouseSensitivityPreference.Resolve(Profile),fps.Visible);
            pendingSeat=-1;RefreshInterface();Select(start);
        }
        bool IdentitiesReady()
        {
            if(reviewComposition!=null||combatReview)return true;
            for(int i=0;i<LocalSeatCount;i++)if(input.IsHumanSeat(i)&&!identities.HasIdentity(i))return false;
            return true;
        }
        string IdentityLabel(int seat)
        {
            var id=identities.ProfileId(seat);
            if(id!=null)return playerProfiles.Find(id)?.Name??"Профиль удалён";
            return identities.GuestAt(seat)!=null?"Гость":"выберите профиль";
        }
        NativeMatchComposition WithSelectedNames(NativeMatchComposition original)
        {
            var snapshot=original.Read();
            for(int seat=0;seat<original.LocalCount;seat++)
            {
                int participant=original.ParticipantAt(seat);
                if(snapshot.Participants[participant].Kind!=NativeParticipantKind.LocalHuman)continue;
                var info=snapshot.Participants[participant];info.Name=IdentityLabel(seat);snapshot.Participants[participant]=info;
            }
            return NativeMatchComposition.Restore(snapshot);
        }
        void RefreshProfilesUi()
        {
            if(profileList==null)return;
            foreach(Transform child in profileList){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var profiles=playerProfiles.Profiles.ToArray();
            for(int i=0;i<profiles.Length;i++)
            {
                var record=profiles[i];float top=1f-i/Mathf.Max(4f,profiles.Length);
                MenuButton(profileList,"profile-"+record.Id,record.Name,new Vector2(0,top-1f/Mathf.Max(4f,profiles.Length)+.012f),new Vector2(1,top-.012f),()=>SelectProfile(record.Id));
            }
            if(selectedProfileId==null||playerProfiles.Find(selectedProfileId)==null)selectedProfileId=profiles.FirstOrDefault()?.Id;
            RefreshProfileDetail();
        }
        void SelectProfile(string id) { selectedProfileId=id;RefreshProfileDetail(); }
        void RefreshProfileDetail()
        {
            var selected=playerProfiles.Find(selectedProfileId);
            profileDetail.text=selected==null?"Создайте профиль":selected.Name+"\nМышь: "+selected.MouseDegreesPerPixel.ToString("0.00")+"°/px\nFPS: "+(selected.ShowFps?"вкл":"выкл");
            profileNameInput.text=selected?.Name??"";
        }
        void CreateNamedProfile()
        {
            var name=(profileNameInput.text??"").Trim();
            if(name.Length==0||playerProfiles.Find(selectedProfileId)!=null&&name==playerProfiles.Find(selectedProfileId).Name)name="Игрок "+(playerProfiles.Profiles.Count+1);
            var selected=playerProfiles.Create(name,MouseSensitivityPreference.Resolve(Profile),fps.Visible);
            selectedProfileId=selected.Id;RefreshProfilesUi();
        }
        void RenameSelectedProfile()
        {
            if(selectedProfileId==null)return;
            try {playerProfiles.Rename(selectedProfileId,profileNameInput.text);RefreshProfilesUi();}
            catch(ArgumentException){profileDetail.text="Имя должно содержать 1–24 символа";}
        }
        void DeleteSelectedProfile()
        {
            if(selectedProfileId==null)return;
            playerProfiles.Delete(selectedProfileId);identities.ForgetProfile(selectedProfileId);selectedProfileId=null;
            RefreshProfilesUi();RefreshInterface();
        }
        void StepSelectedProfile(int direction)
        {
            var selected=playerProfiles.Find(selectedProfileId);if(selected==null)return;
            var descriptor=MouseSensitivityPreference.Descriptor(Profile);
            float value=Mathf.Clamp(selected.MouseDegreesPerPixel+direction*descriptor.Step,descriptor.Minimum,descriptor.Maximum);
            playerProfiles.SetPersonal(selected.Id,value,selected.ShowFps);RefreshProfileDetail();
        }
        void ToggleSelectedProfileFps()
        {
            var selected=playerProfiles.Find(selectedProfileId);if(selected==null)return;
            playerProfiles.SetPersonal(selected.Id,selected.MouseDegreesPerPixel,!selected.ShowFps);RefreshProfileDetail();
        }
        void StepMenuSensitivity(int direction)
        {
            if(settingsSeat<0)MouseSensitivityPreference.Step(Profile,direction);
            else StepPersonalSensitivity(settingsSeat,direction);
            RefreshInterface();
        }
        void ToggleMenuFps()
        {
            if(settingsSeat<0)fps.Toggle();
            else TogglePersonalFps(settingsSeat);
            RefreshInterface();
        }
        void StepPersonalSensitivity(int seat,int direction)
        {
            var id=identities.ProfileId(seat);
            if(id!=null) {selectedProfileId=id;StepSelectedProfile(direction);}
            else
            {
                var guest=identities.GuestAt(seat);
                if(guest!=null){var d=MouseSensitivityPreference.Descriptor(Profile);guest.MouseDegreesPerPixel=Mathf.Clamp(guest.MouseDegreesPerPixel+direction*d.Step,d.Minimum,d.Maximum);}
            }
            RefreshSeatPauseUi();
        }
        void TogglePersonalFps(int seat)
        {
            var id=identities.ProfileId(seat);
            if(id!=null){selectedProfileId=id;ToggleSelectedProfileFps();}
            else {var guest=identities.GuestAt(seat);if(guest!=null)guest.ShowFps=!guest.ShowFps;}
            RefreshSeatPauseUi();
        }
        float ActiveMouseSensitivity()
        {
            for(int i=0;i<LocalSeatCount;i++)if(input.DeviceAt(i) is Keyboard)
            {
                var id=identities.ProfileId(i);if(id!=null)return playerProfiles.Find(id)?.MouseDegreesPerPixel??MouseSensitivityPreference.Resolve(Profile);
                var guest=identities.GuestAt(i);if(guest!=null)return guest.MouseDegreesPerPixel;
            }
            return MouseSensitivityPreference.Resolve(Profile);
        }
        void ApplyMatchFpsPreference()
        {
            if(UseSeatPauseMenus){fps.SetSessionVisibility(false);RefreshSeatPauseUi();return;}
            int operatorSeat=-1;
            for(int i=0;i<LocalSeatCount;i++)if(input.IsHumanSeat(i) && input.DeviceAt(i) is Keyboard){operatorSeat=i;break;}
            if(operatorSeat<0)for(int i=0;i<LocalSeatCount;i++)if(input.IsHumanSeat(i)){operatorSeat=i;break;}
            fps.SetSessionVisibility(operatorSeat<0?PlayerPrefs.GetInt(FpsDisplay.PreferenceKey,1)!=0:PersonalFps(operatorSeat));
        }
        void RefreshLab()
        {
            if(labDetail==null||Profile.Descriptors.Count==0)return;
            var d=Profile.Descriptors[labIndex%Profile.Descriptors.Count];
            labDetail.text=d.Group+" / "+d.Label+"\n"+d.Description+"\n\n"+Profile.Get(d.Path).ToString("0.###")+" "+d.Unit+"\n"+(labIndex+1)+" / "+Profile.Descriptors.Count;
        }
        void StepLab(int direction)
        {
            var d=Profile.Descriptors[labIndex%Profile.Descriptors.Count];
            Profile.Set(d.Path,Mathf.Clamp(Profile.Get(d.Path)+direction*d.Step,d.Minimum,d.Maximum));RefreshLab();
        }
        void RefreshModernMenuUi()
        {
            if(mainMenuScreen==null)return;
            bool shell=phase==Phase.MainMenu||phase==Phase.Profiles||phase==Phase.Settings||phase==Phase.Lab;
            oldMenuColumn.SetActive(!shell && pendingSeat<0);
            mainMenuScreen.SetActive(phase==Phase.MainMenu);
            profilesScreen.SetActive(phase==Phase.Profiles);
            settingsScreen.SetActive(phase==Phase.Settings);
            labScreen.SetActive(phase==Phase.Lab);
            identityScreen.SetActive(phase==Phase.Setup && pendingSeat>=0);
            for(int seat=0;seat<seatIdentityButtons.Length;seat++)
            {
                seatIdentityButtons[seat].gameObject.SetActive(phase==Phase.Setup && pendingSeat<0 && seat<LocalSeatCount && input.IsHumanSeat(seat));
                seatIdentityButtons[seat].GetComponentInChildren<Text>().text="Игрок "+(seat+1)+" · "+IdentityLabel(seat)+" · "+
                    (input.DeviceAt(seat)==null?"Ожидает устройство":input.IsConnected(seat)?input.Label(seat):"Устройство отключено — назначьте заново");
            }
            if(seatPanel)seatPanel.SetActive(phase==Phase.Setup && pendingSeat<0);
            if(botPanel)botPanel.SetActive(phase==Phase.Setup && pendingSeat<0);
            if(phase==Phase.Settings)
            {
                float sensitivity=settingsSeat<0?MouseSensitivityPreference.Resolve(Profile):PersonalSensitivity(settingsSeat);
                bool showFps=settingsSeat<0?fps.Visible:PersonalFps(settingsSeat);
                settingsHeading.text=(settingsSeat<0?"НАСТРОЙКИ ИГРЫ":"НАСТРОЙКИ · "+IdentityLabel(settingsSeat))+
                    "\nМышь: "+sensitivity.ToString("0.00")+"°/px · FPS: "+(showFps?"вкл":"выкл");
            }
        }
        float PersonalSensitivity(int seat)
        {
            var id=identities.ProfileId(seat);
            return id!=null?playerProfiles.Find(id)?.MouseDegreesPerPixel??MouseSensitivityPreference.Resolve(Profile):
                identities.GuestAt(seat)?.MouseDegreesPerPixel??MouseSensitivityPreference.Resolve(Profile);
        }
        bool PersonalFps(int seat)
        {
            var id=identities.ProfileId(seat);
            return id!=null?playerProfiles.Find(id)?.ShowFps??fps.Visible:identities.GuestAt(seat)?.ShowFps??fps.Visible;
        }
    }
}

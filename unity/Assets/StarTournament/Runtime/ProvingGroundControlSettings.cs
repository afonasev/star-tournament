using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        const string KeyboardHelp="КЛАВИАТУРА / МЫШЬ\n\nW A S D — движение\nМышь — взгляд\nПробел — прыжок\nЛКМ — огонь (удержание для Резака)\n1 / 2 / 3 / 4 — винтовка / дробовик / Pulse / Резак\nQ / E, колесо — предыдущее / следующее оружие\nTab (удерживать) — таблица результатов\nEsc — пауза / назад\nПробел в подготовке — присоединиться\n\nМЕНЮ\nСтрелки — выбор; Enter — подтвердить\n← / → — значение слайдера\nМышь — выбор, перетаскивание слайдера\nВ личной паузе Backspace — назад";
        const string GamepadHelp="ГЕЙМПАД  ·  XBOX / PLAYSTATION\n\nЛевый стик — движение\nПравый стик — взгляд\nA / × (нижняя кнопка) — прыжок\nRT / R2 — огонь (удержание для Резака)\nD-pad ← / ↑ / → / ↓ — винтовка / дробовик / Pulse / Резак\nView / Share / Create (удерживать) — таблица результатов\nMenu / Start / Options — пауза\nY / △ — присоединиться в подготовке матча\n\nМЕНЮ\nD-pad / левый стик — выбор\n← / → — значение слайдера\nA / × — выбрать / изменить чекбокс\nB / ○ — назад / отменить";
        string settingsProfileId;
        Slider settingsMouseSlider,settingsHorizontalSlider,settingsVerticalSlider;
        Toggle settingsAutoLevelToggle,settingsFpsToggle,settingsShadowsToggle;
        GameObject settingsHelpPage;
        Button settingsHelpButton,settingsHelpBack;
        bool settingsHelpOpen;

        Slider NumericSetting(Transform parent,string name,string title,Vector2 min,Vector2 max,NumericDescriptor d,UnityEngine.Events.UnityAction<float> changed,int font=22)
        {
            var row=Panel(parent,name+"-row",min,max,MenuCard);
            var value=Label(row.transform,"value",title,font,new Vector2(.04f,.51f),new Vector2(.96f,.98f),TextAnchor.MiddleLeft,Color.white);
            var track=Panel(row.transform,name,new Vector2(.05f,.16f),new Vector2(.95f,.42f),new Color32(67,89,101,255));
            var slider=track.AddComponent<SettingsSlider>();slider.minValue=d.Minimum;slider.maxValue=d.Maximum;slider.Step=d.Step;
            var fill=Panel(track.transform,"fill",Vector2.zero,Vector2.one,MenuGold);fill.GetComponent<Image>().raycastTarget=false;slider.fillRect=(RectTransform)fill.transform;
            var area=Panel(track.transform,"handle-area",Vector2.zero,Vector2.one,Color.clear);area.GetComponent<Image>().raycastTarget=false;
            var handle=Panel(area.transform,"handle",Vector2.zero,Vector2.one,Color.white);var rect=(RectTransform)handle.transform;rect.sizeDelta=new Vector2(18,10);
            slider.handleRect=rect;slider.targetGraphic=handle.GetComponent<Image>();
            slider.onValueChanged.AddListener(v=>{var snapped=GamepadLookSettings.Snap(Profile,d.Path,v);slider.SetValueWithoutNotify(snapped);changed(snapped);});
            return slider;
        }
        Toggle BooleanSetting(Transform parent,string name,string title,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction<bool> changed,int font=22)
        {
            var row=Panel(parent,name,min,max,MenuCard);var toggle=row.AddComponent<Toggle>();
            var box=Panel(row.transform,"box",new Vector2(.04f,.18f),new Vector2(.10f,.82f),new Color32(79,102,114,255));
            var mark=Label(box.transform,"check","✓",font,Vector2.zero,Vector2.one,TextAnchor.MiddleCenter,MenuInk);
            var fill=Panel(box.transform,"check-fill",new Vector2(.08f,.08f),new Vector2(.92f,.92f),MenuGold);
            mark.transform.SetAsLastSibling();toggle.graphic=fill.GetComponent<Image>();toggle.targetGraphic=box.GetComponent<Image>();
            // The filled gold box is the checked-state marker; decorative glyph follows it.
            toggle.onValueChanged.AddListener(v=>{mark.gameObject.SetActive(v);changed(v);});
            Label(row.transform,"label",title,font,new Vector2(.14f,.05f),new Vector2(.96f,.95f),TextAnchor.MiddleLeft,Color.white);
            return toggle;
        }
        static void RefreshNumericSetting(Slider slider,string title,float value,string unit)
        {slider.SetValueWithoutNotify(value);slider.transform.parent.Find("value").GetComponent<Text>().text=title+"   "+value.ToString("0.##")+" "+unit;}
        static void RefreshToggle(Toggle toggle,bool value)
        {toggle.SetIsOnWithoutNotify(value);toggle.transform.Find("box/check").gameObject.SetActive(value);}
        GamepadLookSettings PersonalGamepadLook(int seat)
        {
            var id=identities.ProfileId(seat);
            if(id!=null)return GamepadLookSettings.Resolve(frozenMovement??Profile,playerProfiles.Find(id));
            var guest=identities.GuestAt(seat);
            if(guest!=null){if(!guest.GamepadLook.HasValue)guest.GamepadLook=GamepadLookSettings.General(frozenMovement??Profile);return guest.GamepadLook.Value;}
            return GamepadLookSettings.General(frozenMovement??Profile);
        }
        GamepadLookSettings MenuGamepadLook()=>settingsProfileId!=null?GamepadLookSettings.Resolve(Profile,playerProfiles.Find(settingsProfileId)):
            settingsSeat<0?GamepadLookSettings.General(Profile):PersonalGamepadLook(settingsSeat);
        void SavePersonalGamepad(int seat,GamepadLookSettings value)
        {var id=identities.ProfileId(seat);if(id!=null)playerProfiles.SetGamepad(id,value);else if(identities.GuestAt(seat)!=null)identities.GuestAt(seat).GamepadLook=value;}
        void SetMenuGamepad(int axis,float value,bool enabled=false)
        {
            var s=MenuGamepadLook();if(axis==0)s.Horizontal=value;else if(axis==1)s.Vertical=value;else s.AutoLevel=enabled;
            if(settingsProfileId!=null)playerProfiles.SetGamepad(settingsProfileId,s);
            else if(settingsSeat<0)GamepadLookSettings.SaveGeneral(s);else SavePersonalGamepad(settingsSeat,s);
            RefreshSettingsUi();
        }
        void SetPersonalMouse(int seat,float value)
        {var id=identities.ProfileId(seat);if(id!=null){var r=playerProfiles.Find(id);playerProfiles.SetPersonal(id,value,r.ShowFps);}else if(identities.GuestAt(seat)!=null)identities.GuestAt(seat).MouseDegreesPerPixel=value;}
        float MenuMouseSensitivity()=>settingsProfileId!=null?playerProfiles.Find(settingsProfileId).MouseDegreesPerPixel:
            settingsSeat<0?MouseSensitivityPreference.Resolve(Profile):PersonalSensitivity(settingsSeat);
        bool MenuFps()=>settingsProfileId!=null?playerProfiles.Find(settingsProfileId).ShowFps:settingsSeat<0?fps.Visible:PersonalFps(settingsSeat);
        void SetMenuMouse(float value)
        {
            if(settingsProfileId!=null){var r=playerProfiles.Find(settingsProfileId);playerProfiles.SetPersonal(r.Id,value,r.ShowFps);}
            else if(settingsSeat<0)MouseSensitivityPreference.Set(Profile,value);else SetPersonalMouse(settingsSeat,value);
            RefreshSettingsUi();
        }
        void SetMenuFps(bool value)
        {if(MenuFps()==value)return;if(settingsProfileId!=null){var r=playerProfiles.Find(settingsProfileId);playerProfiles.SetPersonal(r.Id,r.MouseDegreesPerPixel,value);}else ToggleMenuFps();RefreshSettingsUi();}
        void OpenProfileSettings()
        {if(selectedProfileId==null)return;OpenSettings(-1);settingsProfileId=selectedProfileId;SelectSettingsSection(SettingsSection.Control);}
        PlayerProfileRecord CreateProfileWithGeneralSettings(string name)
        {
            var record=playerProfiles.Create(name,MouseSensitivityPreference.Resolve(Profile),fps.Visible);
            playerProfiles.SetGamepad(record.Id,GamepadLookSettings.General(Profile));return record;
        }
        void SetHelp(bool open)
        {settingsHelpOpen=open;RefreshSettingsUi();Select(open?settingsHelpBack:settingsHelpButton);}
    }
}

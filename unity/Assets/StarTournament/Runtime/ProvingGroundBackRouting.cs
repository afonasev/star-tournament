using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        const float MenuHistoryScrollDeadzone=.15f;
        int menuBackConsumedFrame=-1;
        bool TryTakeMenuBack(Gamepad assigned=null,bool restrict=false)
        {
            if(!menuWindowFocused||menuBackConsumedFrame==Time.frameCount)return false;
            bool back=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame;
            back|=restrict?assigned!=null&&assigned.added&&assigned.enabled&&assigned.buttonEast.wasPressedThisFrame:
                Gamepad.all.Any(p=>p.added&&p.enabled&&p.buttonEast.wasPressedThisFrame);
            if(!back)return false;menuBackConsumedFrame=Time.frameCount;return true;
        }
        void UpdateShellBackInput()
        {
            if(phase==Phase.Profiles)
            {if(TryTakeMenuBack()&&profilesBackButton.IsInteractable())profilesBackButton.onClick.Invoke();}
            else if(phase==Phase.Paused&&!UseSeatPauseMenus)
            {if(TryTakeMenuBack()&&FocusAllowsResume&&resume.IsInteractable())resume.onClick.Invoke();}
        }
        void BindMenuShortcut(Selectable control,MenuPadButton key,Func<bool> available)
        {
            var hint=control.GetComponent<MenuActionHint>();if(!hint)return;
            hint.Shortcut=key;hint.ShortcutActive=()=>!textEntryPanel&&!textClosing&&available();
        }
        void CloseIdentityPicker()
        {pendingSeat=-1;RefreshInterface();FocusRosterCard(0);}
    }
}

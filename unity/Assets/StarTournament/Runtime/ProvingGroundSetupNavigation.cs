using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        public const string LastPlayedMapPreferenceKey="star-tournament.last-played-map";
        static readonly string[] SetupMapIds={CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id};
        int setupTransitionFrame=-1;
        bool setupGamepadNavigation=true;

        void SetSetupNavigationMode(bool gamepad)
        {
            bool changed=setupGamepadNavigation!=gamepad;
            setupGamepadNavigation=gamepad;if(arenaChoices==null)return;
            if(changed){ConfigureRosterNavigation();ConfigureSetupNavigation();}
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(gamepad && selected!=null && (setupSteps.Any(tab=>tab.gameObject==selected) ||
                selected==setupPrevious.gameObject || selected==setupNext.gameObject || selected==start.gameObject))FocusSetupStep();
        }
        void TrackSetupNavigationInput(InputAction.CallbackContext context)
        {
            if(context.action==menuMoveAction && context.ReadValue<Vector2>()!=Vector2.zero ||
                context.action==menuSubmitAction && context.ReadValueAsButton())
            {
                var device=context.control?.device;
                if(device is Gamepad || device is Keyboard)SetSetupNavigationMode(device is Gamepad);
            }
        }

        void RestoreLastPlayedMap()
        {
            string saved=PlayerPrefs.GetString(LastPlayedMapPreferenceKey,CombatBowlCatalog.Id);
            SelectedMapId=SetupMapIds.Contains(saved)?saved:CombatBowlCatalog.Id;
        }
        void RememberPlayedMap()
        {
            PlayerPrefs.SetString(LastPlayedMapPreferenceKey,SelectedMapId);PlayerPrefs.Save();
        }
        void FocusSetupStep()
        {
            if(phase!=Phase.Setup || pendingSeat>=0 || rosterEditing>=0)return;
            if(!setupGamepadNavigation){Select(setupStep==2?start:setupNext);return;}
            if(setupStep==0)Select(arenaChoices[System.Array.IndexOf(SetupMapIds,SelectedMapId)]);
            else if(setupStep==1)Select(modeChoices[0]);
            else FocusRosterCard(0);
        }
        void AdvanceSetupInput()
        {
            if(phase!=Phase.Setup || rosterEditing>=0 || pendingSeat>=0 || setupTransitionFrame==Time.frameCount)return;
            if(setupStep==2 && (!input.Ready || !IdentitiesReady() || !ValidSetup()))return;
            setupTransitionFrame=Time.frameCount;
            if(setupStep<2)NextSetupStep();else start.onClick.Invoke();
        }
        static void NoSetupNavigation(Selectable button)
        {
            button.navigation=new Navigation{mode=Navigation.Mode.None};
        }
        static void SetupNavigation(Selectable control,Selectable up,Selectable down,Selectable left,Selectable right)
        {
            control.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down,selectOnLeft=left,selectOnRight=right};
        }
        void ConfigureSetupNavigation()
        {
            if(arenaChoices==null)return;
            var durationMinus=setupRulesPage.transform.Find("duration-minus").GetComponent<Button>();
            if(!setupGamepadNavigation)
            {
                var controls=arenaChoices.Concat(modeChoices).Concat(new[]{durationMinus,durationButton,targetMinus,targetPlus});
                if(setupStep!=2)controls=controls.Concat(setupSteps).Concat(new[]{setupPrevious,setupNext,start});
                foreach(var control in controls)control.navigation=new Navigation{mode=Navigation.Mode.Automatic};
                return;
            }
            foreach(var tab in setupSteps)NoSetupNavigation(tab);
            NoSetupNavigation(setupPrevious);NoSetupNavigation(setupNext);NoSetupNavigation(start);
            for(int i=0;i<arenaChoices.Length;i++)
            {
                var previous=arenaChoices[System.Math.Max(0,i-1)];var next=arenaChoices[System.Math.Min(arenaChoices.Length-1,i+1)];
                SetupNavigation(arenaChoices[i],previous,next,previous,next);
            }
            var rows=new[]{modeChoices,new[]{durationMinus,durationButton},new[]{targetMinus,targetPlus}}
                .Select(row=>row.Where(b=>b.gameObject.activeInHierarchy&&b.interactable).ToArray()).Where(row=>row.Length>0).ToArray();
            for(int row=0;row<rows.Length;row++)for(int col=0;col<rows[row].Length;col++)
            {
                var current=rows[row][col];var above=rows[System.Math.Max(0,row-1)];var below=rows[System.Math.Min(rows.Length-1,row+1)];
                SetupNavigation(current,above[System.Math.Min(col,above.Length-1)],below[System.Math.Min(col,below.Length-1)],
                    rows[row][System.Math.Max(0,col-1)],rows[row][System.Math.Min(rows[row].Length-1,col+1)]);
            }
        }
    }
}

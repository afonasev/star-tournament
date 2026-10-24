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
        static readonly string[] SetupMapIds={CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id};
        int setupTransitionFrame=-1;
        bool setupGamepadNavigation=true;

        void SetSetupNavigationMode(bool gamepad) { setupGamepadNavigation=gamepad; }
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
        void FocusSetupStep() { if(phase==Phase.Setup&&!LobbyModal)Select(lobbyMap); }
        bool CanAdvanceSetupInput()=>phase==Phase.Setup&&!LobbyModal&&setupTransitionFrame!=Time.frameCount&&
            (setupStep!=2||(input.Ready&&IdentitiesReady()&&ValidSetup()));
        void AdvanceSetupInput()
        {
            if(!CanAdvanceSetupInput())return;
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
        void ConfigureSetupNavigation() { ConfigureLobbyNavigation(); }
    }
}

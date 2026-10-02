using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        readonly Text[] damageBonusNotice=new Text[SeatInputCoordinator.SeatCount];
        NativeCombatSession damageBonusSession;
        string damageBonusText="";
        double damageBonusUntil;
        void BindDamageBonusAlerts()
        {
            UnbindDamageBonusAlerts();damageBonusSession=Session;ClearDamageBonusAlert();
            foreach(var label in damageBonusNotice)if(label)ConfigureDamageBonusNotice(label);
            if(damageBonusSession==null)return;
            damageBonusSession.DamageBonusAppeared+=ShowDamageBonusSpawn;
            damageBonusSession.PickupCollected+=ShowDamageBonusPickup;
            damageBonusSession.StateRestored+=ClearDamageBonusAlert;
        }
        void UnbindDamageBonusAlerts()
        {
            if(damageBonusSession==null)return;
            damageBonusSession.DamageBonusAppeared-=ShowDamageBonusSpawn;
            damageBonusSession.PickupCollected-=ShowDamageBonusPickup;
            damageBonusSession.StateRestored-=ClearDamageBonusAlert;
            damageBonusSession=null;
        }
        void ClearDamageBonusAlert(){damageBonusText="";damageBonusUntil=0;foreach(var label in damageBonusNotice)if(label)label.text="";}
        void ShowDamageBonusSpawn()=>ShowDamageBonusAlert("ПОЯВИЛСЯ БОНУС УРОНА");
        void ShowDamageBonusPickup(int participant,string id,NativeBotPickupKind kind)
        {
            if(kind==NativeBotPickupKind.Damage)ShowDamageBonusAlert("БОНУС УРОНА ПОДОБРАН");
        }
        void ShowDamageBonusAlert(string message)
        {
            damageBonusText=message;damageBonusUntil=Session.Time+Profile.Get("ui.damageBonusSeconds");
        }
        void ConfigureDamageBonusNotice(Text label)
        {
            // Refresh at binding: the selected immutable Lab revision applies to the next match.
            label.fontSize=(int)Profile.Get("ui.damageBonusFontSize");
            float top=1-Profile.Get("ui.damageBonusTopInset");
            // The alert spans the viewport width; it cannot own focus or input.
            Layout(label.rectTransform,new Vector2(0,top-Profile.Get("ui.damageBonusHeight")),new Vector2(1,top));
        }
        void CreateDamageBonusNotice(int seat)
        {
            var label=TextElement(viewportRoots[seat],"damage-bonus-notice-"+seat,"",(int)Profile.Get("ui.damageBonusFontSize"));
            ConfigureDamageBonusNotice(label);
            label.alignment=TextAnchor.MiddleCenter;label.color=Color.red;label.fontStyle=FontStyle.Bold;
            label.supportRichText=false;label.raycastTarget=false;
            var outline=label.gameObject.AddComponent<Outline>();outline.effectColor=Color.black;
            damageBonusNotice[seat]=label;
        }
    }
}

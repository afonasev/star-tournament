using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        readonly Text[] botBanterNotice=new Text[SeatInputCoordinator.SeatCount];
        NativeBotBanter botBanter;
        void BindBotBanter()
        {
            UnbindBotBanter();
            botBanter=new NativeBotBanter(Session,Composition,gameObject.scene.GetPhysicsScene(),
                (frozenMovement??Profile).Get("camera.eyeHeight"),(frozenLife??LifeProfile).Get("combat.maximumHealth"),unchecked((int)botSeed));
            botBanter.Banter.LineSelected+=OnBotBanterLineSelected;
        }
        void UnbindBotBanter()
        {
            if(botBanter!=null)botBanter.Banter.LineSelected-=OnBotBanterLineSelected;
            botBanter?.Dispose();botBanter=null;gameAudio?.StopBotReaction();
            foreach(var label in botBanterNotice)if(label)label.text="";
        }
        void OnBotBanterLineSelected(BotBanterLine line)
        {
            if(phase==Phase.Running&&Session.Match?.Phase!=NativeMatchPhase.Finished)
                gameAudio?.PlayBotReaction(line.Text,line.Speaker,line.SpeakerMayBeDead);
        }
        void CreateBotBanterNotice(int seat)
        {
            var label=TextElement(viewportRoots[seat],"bot-banter-notice-"+seat,"",22);
            // Bottom-left, above the name/health row, away from centre kill/death feedback.
            Layout(label.rectTransform,new Vector2(.025f,.15f),new Vector2(.975f,.25f));
            label.alignment=TextAnchor.MiddleLeft;label.color=new Color32(255,223,145,255);
            label.resizeTextForBestFit=true;label.resizeTextMinSize=14;label.resizeTextMaxSize=22;
            label.supportRichText=false;label.raycastTarget=false;
            var outline=label.gameObject.AddComponent<Outline>();outline.effectColor=Color.black;
            botBanterNotice[seat]=label;
        }
        void RefreshBotBanterNotice(int seat)
        {
            var label=botBanterNotice[seat];if(!label)return;
            var line=botBanter?.Banter.Current;
            if(!NativeBotReactionPreferences.TextEnabled||phase!=Phase.Running||!line.HasValue||Session.Time>=line.Value.Until||
                !line.Value.SpeakerMayBeDead&&Session.Life(line.Value.Speaker).Dead)
            { label.text="";return; }
            string speaker=Composition.Participant(line.Value.Speaker).Name;
            string target=line.Value.Target<0?"":" → "+Composition.Participant(line.Value.Target).Name;
            label.text=speaker+target+": «"+line.Value.Text+"»";
        }
    }
}

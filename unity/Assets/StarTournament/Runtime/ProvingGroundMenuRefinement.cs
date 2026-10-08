using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        int lastBotDifficulty=(int)NativeBotDifficulty.Normal;
        Button[] arenaChoices,modeChoices;
        Text durationValue,targetValue;
        void ApplyModeTarget()
        {
            var c=Configuration;c.TargetEnabled=true;
            c.TargetPoints=(int)MatchProfile.Get(SetupMode==NativeMatchMode.Teams?"match.teamTargetDefault":"match.ffaTargetDefault");Configuration=c;
        }
        void PollRosterShortcuts()
        {
            if(setupStep!=2||rosterEditing>=0||pendingSeat>=0)return;
            foreach(var pad in Gamepad.all)
            {
                if(pad.buttonNorth.wasPressedThisFrame||pad.buttonWest.wasPressedThisFrame)SetSetupNavigationMode(true);
                int seat=Enumerable.Range(0,LocalSeatCount).Where(i=>input.DeviceAt(i)==pad).DefaultIfEmpty(-1).First();
                if(seat>=0&&pad.buttonNorth.wasPressedThisFrame&&SetupMode==NativeMatchMode.Teams)
                    SetTeam(seat,teamAssignments[seat]==NativeTeam.TeamA?NativeTeam.TeamB:NativeTeam.TeamA);
                if(pad.buttonWest.wasPressedThisFrame)AddBot();
            }
        }
        void PollSetupStart()
        {
            if(Gamepad.all.Any(pad=>pad.startButton.wasPressedThisFrame)){SetSetupNavigationMode(true);AdvanceSetupInput();}
        }
        void StyleOperatorPause()
        {
            if(oldMenuColumn==null)return;
            overlay.GetComponent<Image>().color=phase==Phase.Paused?new Color32(4,12,21,175):new Color32(10,18,30,245);
            var group=oldMenuColumn.GetComponent<VerticalLayoutGroup>();
            var background=oldMenuColumn.GetComponent<Image>();
            var previousIdentity=oldMenuColumn.transform.Find("operator-identity");if(previousIdentity)previousIdentity.gameObject.SetActive(phase==Phase.Paused&&!UseSeatPauseMenus);
            if(phase!=Phase.Paused||UseSeatPauseMenus){if(background)background.enabled=false;group.enabled=true;return;}
            group.enabled=false;if(!background)background=oldMenuColumn.AddComponent<Image>();background.enabled=true;background.color=new Color32(20,33,48,248);RoundRosterPanel(oldMenuColumn);
            var pauseRect=(RectTransform)oldMenuColumn.transform;var viewport=(RectTransform)pauseRect.parent;
            pauseRect.anchorMin=pauseRect.anchorMax=new Vector2(.5f,.5f);pauseRect.sizeDelta=new Vector2(Mathf.Min(viewport.rect.width*.78f,760),Mathf.Min(viewport.rect.height*.82f,650));pauseRect.anchoredPosition=Vector2.zero;
            status.text="Пауза";status.fontSize=40;status.color=MenuGold;Layout(status.rectTransform,new Vector2(.08f,.83f),new Vector2(.92f,.96f));
            var identity=oldMenuColumn.transform.Find("operator-identity");
            if(!identity)identity=Label(oldMenuColumn.transform,"operator-identity","",30,new Vector2(.08f,.72f),new Vector2(.92f,.83f),TextAnchor.MiddleCenter,Color.white).transform;
            int seat=Enumerable.Range(0,LocalSeatCount).Where(input.IsHumanSeat).DefaultIfEmpty(-1).First();identity.GetComponent<Text>().text=seat>=0?IdentityLabel(seat):"";
            var buttons=new[]{resume,fallbackSettings,repeat,menu};
            for(int i=0;i<buttons.Length;i++){float top=.70f-i*.155f;Layout((RectTransform)buttons[i].transform,new Vector2(.08f,top-.11f),new Vector2(.92f,top));}
        }
        void CreateF2Setup()
        {
            var ids=SetupMapIds;
            arenaChoices=new Button[ids.Length];
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];float top=.71f-i*.115f;
                arenaChoices[i]=MenuButton(setupMapPage.transform,"arena-choice-"+i,AuthoredArenaCatalog.Name(id),new Vector2(.04f,top-.08f),new Vector2(.32f,top),()=>{SelectAuthoredMap(id);setupMaxStep=Mathf.Max(1,setupMaxStep);RefreshInterface();});
                arenaChoices[i].gameObject.AddComponent<SetupMapChoice>().Initialize(
                    ()=>{if(setupGamepadNavigation&&phase==Phase.Setup&&setupStep==0&&SelectedMapId!=id)SelectAuthoredMap(id);},
                    ()=>{if(setupGamepadNavigation&&setupStep==0)AdvanceSetupInput();});
            }
            modeChoices=new Button[2];
            for(int i=0;i<2;i++)
            {
                var mode=i==0?NativeMatchMode.Ffa:NativeMatchMode.Teams;
                modeChoices[i]=MenuButton(setupRulesPage.transform,"mode-choice-"+i,i==0?"Каждый сам за себя":"Командный бой",new Vector2(.05f+i*.31f,.70f),new Vector2(.34f+i*.31f,.79f),()=>{SetMatchMode(mode);setupMaxStep=2;RefreshInterface();});
            }
            durationValue=Label(setupRulesPage.transform,"duration-value","",28,new Vector2(.05f,.47f),new Vector2(.35f,.56f),TextAnchor.MiddleLeft,Color.white);
            targetValue=Label(setupRulesPage.transform,"target-value","",28,new Vector2(.05f,.28f),new Vector2(.35f,.37f),TextAnchor.MiddleLeft,Color.white);
        }
        void RefreshF2Setup()
        {
            if(arenaChoices==null)return;
            var content=(RectTransform)setupPlayersPage.transform.parent;Layout(content,new Vector2(.05f,.16f),new Vector2(.95f,.76f));
            var summary=setupSummary.transform.parent;summary.GetComponent<Image>().enabled=false;
            Layout((RectTransform)summary,new Vector2(.05f,.045f),new Vector2(.95f,.13f));
            summary.Find("summary-title").gameObject.SetActive(false);setupSummary.gameObject.SetActive(false);setupLabIdentity.gameObject.SetActive(false);
            Layout(setupMessage.rectTransform,new Vector2(0,0),new Vector2(.68f,1));
            Layout((RectTransform)setupNext.transform,new Vector2(.76f,0),new Vector2(1,1));Layout((RectTransform)start.transform,new Vector2(.76f,0),new Vector2(1,1));
            setupSteps[0].GetComponentInChildren<Text>().text=AuthoredArenaCatalog.Name(SelectedMapId);
            setupSteps[1].GetComponentInChildren<Text>().text=(SetupMode==NativeMatchMode.Teams?"Командный бой":"Каждый сам за себя");
            for(int i=0;i<3;i++)Layout((RectTransform)setupSteps[i].transform,new Vector2(.05f+i*.305f,.78f),new Vector2(.34f+i*.305f,.85f));
            rosterPageTitle.gameObject.SetActive(false);
            arenaButton.gameObject.SetActive(false);modeButton.gameObject.SetActive(false);targetButton.gameObject.SetActive(false);
            var card=setupMapPage.transform.Find("selected-map");Layout((RectTransform)card,new Vector2(.37f,.07f),new Vector2(.96f,.81f));
            Layout((RectTransform)card.Find("map-preview"),new Vector2(.05f,.34f),new Vector2(.95f,.98f));
            Layout((RectTransform)card.Find("map-name"),new Vector2(.07f,.20f),new Vector2(.93f,.34f));
            Layout((RectTransform)card.Find("map-revision"),new Vector2(.07f,.02f),new Vector2(.93f,.20f));
            for(int i=0;i<arenaChoices.Length;i++)
            {bool selected=AuthoredArenaCatalog.Name(SelectedMapId)==arenaChoices[i].GetComponentInChildren<Text>().text;arenaChoices[i].GetComponent<Image>().color=selected?MenuGold:MenuCard;arenaChoices[i].GetComponentInChildren<Text>().color=selected?MenuInk:Color.white;}
            for(int i=0;i<2;i++){bool active=i==(SetupMode==NativeMatchMode.Teams?1:0);modeChoices[i].GetComponent<Image>().color=active?MenuGold:MenuCard;modeChoices[i].GetComponentInChildren<Text>().color=active?MenuInk:Color.white;}
            durationValue.text="Длительность: "+Configuration.DurationMinutes+" мин";targetValue.text="Цель: "+Configuration.TargetPoints;
            var minus=setupRulesPage.transform.Find("duration-minus");Layout((RectTransform)minus,new Vector2(.37f,.47f),new Vector2(.43f,.56f));
            Layout((RectTransform)durationButton.transform,new Vector2(.45f,.47f),new Vector2(.51f,.56f));durationButton.GetComponentInChildren<Text>().text="+";
            Layout((RectTransform)targetMinus.transform,new Vector2(.37f,.28f),new Vector2(.43f,.37f));targetMinus.GetComponentInChildren<Text>().text="−";
            Layout((RectTransform)targetPlus.transform,new Vector2(.45f,.28f),new Vector2(.51f,.37f));targetPlus.GetComponentInChildren<Text>().text="+";
            setupRulesPage.transform.Find("target-help").GetComponent<Text>().text="Победа по очкам или по времени. Шаг: 100 очков.";
            setupScreen.transform.Find("setup-description").GetComponent<Text>().text=setupStep==0?
                "Стрелки — выбрать карту · A / Start — далее · B — меню":setupStep==1?
                "Стрелки — выбрать настройку · A — изменить · Start — далее · B — назад":
                "A — изменить участника · X — бот · Y — игрок / команда · Start — начать · B — назад";
            setupNext.GetComponentInChildren<Text>().text=setupStep==0?"ДАЛЕЕ · A / Start":"ДАЛЕЕ · Start";
        }
    }
}

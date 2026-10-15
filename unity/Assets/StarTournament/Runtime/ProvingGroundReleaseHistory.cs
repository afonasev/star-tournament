using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingGround
    {
        GameObject historyScreen,historyVersions;
        Button historyAction,historyBack;
        Text historyTitle,historyStatus,historyBody;
        ScrollRect historyScroll;
        readonly List<Text> historyTextChunks=new List<Text>();
        readonly List<Button> historyButtons=new List<Button>();
        string historyShown,historySelected;
        NativeUpdateBridge.History historyData;
        void CreateReleaseHistoryUi(Transform parent)
        {
            historyAction=MenuButton(mainMenuScreen.transform,"release-history-action","Что нового",new Vector2(.61f,.072f),new Vector2(.78f,.13f),OpenReleaseHistory);
            historyScreen=Panel(parent,"release-history-screen",Vector2.zero,Vector2.one,MenuInk);AddMenuBackdrop(historyScreen,false);
            historyBack=MenuButton(historyScreen.transform,"release-history-back","‹ НАЗАД · B",new Vector2(.05f,.91f),new Vector2(.18f,.96f),CloseReleaseHistory);
            Label(historyScreen.transform,"release-history-heading","ЧТО НОВОГО",40,new Vector2(.21f,.89f),new Vector2(.94f,.96f),TextAnchor.MiddleLeft,MenuGold);
            historyStatus=Label(historyScreen.transform,"release-history-status","",18,new Vector2(.21f,.83f),new Vector2(.94f,.89f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            historyVersions=Panel(historyScreen.transform,"release-history-versions",new Vector2(.05f,.15f),new Vector2(.29f,.81f),MenuCard);
            var detail=Panel(historyScreen.transform,"release-history-detail",new Vector2(.31f,.15f),new Vector2(.95f,.81f),MenuCard);
            historyTitle=Label(detail.transform,"release-history-title","",28,new Vector2(.04f,.86f),new Vector2(.96f,.98f),TextAnchor.MiddleLeft,MenuGold);
            var content=RosterScroll(detail.transform,"release-history-scroll",new Vector2(.04f,.05f),new Vector2(.94f,.84f),out historyScroll);
            var track=Panel(detail.transform,"release-history-scrollbar",new Vector2(.965f,.05f),new Vector2(.975f,.84f),new Color32(48,68,83,255));
            var thumb=Panel(track.transform,"thumb",Vector2.zero,Vector2.one,MenuGold);
            var scrollbar=track.AddComponent<Scrollbar>();scrollbar.handleRect=(RectTransform)thumb.transform;scrollbar.targetGraphic=thumb.GetComponent<Image>();scrollbar.direction=Scrollbar.Direction.BottomToTop;
            historyScroll.verticalScrollbar=scrollbar;historyScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            historyBody=Label(content,"release-history-description","",22,Vector2.zero,Vector2.one,TextAnchor.UpperLeft,Color.white);
            historyBody.supportRichText=false;historyBody.raycastTarget=false;
            historyBody.rectTransform.anchorMin=new Vector2(0,1);historyBody.rectTransform.anchorMax=Vector2.one;historyBody.rectTransform.pivot=new Vector2(.5f,1);
            Label(historyScreen.transform,"release-history-controls","↑ ↓  Версия     A  Читать     Правый стик / PgUp PgDn: прокрутка     B / Esc: назад",18,new Vector2(.05f,.07f),new Vector2(.95f,.12f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            historyScreen.SetActive(false);
        }
        void OpenReleaseHistory()
        {
            if(phase!=Phase.MainMenu)return;
            phase=Phase.History;historyShown=null;RefreshInterface();Select(historyBack);
            updateBridge?.RequestHistory();RefreshReleaseHistory();
        }
        void CloseReleaseHistory()
        {phase=Phase.MainMenu;RefreshInterface();Select(historyAction);}
        void RefreshReleaseHistory()
        {
            var data=updateBridge?updateBridge.HistoryState:null;
            var signature=data==null?"no-launcher":JsonUtility.ToJson(data);
            if(signature==historyShown)return;historyShown=signature;historyData=data;
            historyStatus.text=data==null?(updateBridge&&updateBridge.enabled?"Загрузка истории версий…":"История недоступна. Запустите игру через установленный launcher."):
                data.state=="loading"?"Загрузка истории версий…":!string.IsNullOrEmpty(data.error)?data.error:"Последние версии · GitHub Releases";
            var selectedObject=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            bool ownedFocus=historyButtons.Exists(b=>b&&b.gameObject==selectedObject);
            foreach(var button in historyButtons)if(button)Destroy(button.gameObject);historyButtons.Clear();
            var items=data?.items;int count=items==null?0:Math.Min(11,items.Length);
            for(int i=0;i<count;i++)
            {
                var item=items[i];int index=i;float row=Mathf.Min(.14f,.96f/Math.Max(1,count)),top=.98f-i*row;
                var button=MenuButton(historyVersions.transform,"release-history-version-"+i,item.version+"\n"+HistoryDate(item.date)+(item.installed?" · Установлена":""),new Vector2(.04f,top-row+.006f),new Vector2(.96f,top),()=>ShowReleaseNotes(index));
                button.GetComponent<MenuPresentation>().Height=62;button.GetComponentInChildren<Text>().fontSize=18;historyButtons.Add(button);
            }
            int chosen=count==0?-1:Array.FindIndex(items,0,count,x=>x.version==historySelected);
            if(chosen<0&&count>0)chosen=0;
            if(chosen>=0){ShowReleaseNotes(chosen);if(ownedFocus)Select(historyButtons[chosen]);}
            else{historyTitle.text="";historyBody.text="";}
        }
        static string HistoryDate(string value)=>DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date.ToString("dd.MM.yyyy",CultureInfo.InvariantCulture):value;
        void ShowReleaseNotes(int index)
        {
            var item=historyData.items[index];historySelected=item.version;
            historyTitle.text="Версия "+item.version+" · "+HistoryDate(item.date)+(item.installed?" · Установлена":"");
            // The current game menu is Russian. Both languages are retained in IPC for localization.
            string notes=string.IsNullOrWhiteSpace(item.russian)?"Описание на русском языке не опубликовано для этой версии.":item.russian;
            foreach(var chunk in historyTextChunks)if(chunk)Destroy(chunk.gameObject);historyTextChunks.Clear();
            Canvas.ForceUpdateCanvases();
            float height=0;int start=0,part=0;
            // Keep each uGUI text mesh below the vertex limit without truncating release notes.
            while(start<notes.Length)
            {
                int count=Math.Min(8000,notes.Length-start);
                if(start+count<notes.Length){int newline=notes.LastIndexOf('\n',start+count-1,count);if(newline>=start)count=newline-start+1;}
                var text=part==0?historyBody:Label(historyScroll.content,"release-history-description-"+part,"",22,Vector2.zero,Vector2.one,TextAnchor.UpperLeft,Color.white);
                if(part>0)historyTextChunks.Add(text);
                text.supportRichText=false;text.raycastTarget=false;text.text=notes.Substring(start,count);
                var rect=text.rectTransform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);
                rect.sizeDelta=Vector2.zero;rect.anchoredPosition=new Vector2(0,-height);
                Canvas.ForceUpdateCanvases();float chunkHeight=text.preferredHeight+4;rect.sizeDelta=new Vector2(0,chunkHeight);
                height+=chunkHeight;start+=count;part++;
            }
            historyScroll.content.sizeDelta=new Vector2(0,Mathf.Max(historyScroll.viewport.rect.height,height+20));historyScroll.verticalNormalizedPosition=1;
        }
        void UpdateReleaseHistory()
        {
            if(phase!=Phase.History)return;RefreshReleaseHistory();
            var pad=menuDevice as Gamepad;
            bool back=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame||pad!=null&&pad.buttonEast.wasPressedThisFrame;
            if(back){CloseReleaseHistory();return;}
            float scroll=pad!=null?pad.rightStick.ReadValue().y:0;
            if(Keyboard.current!=null){if(Keyboard.current.pageUpKey.isPressed)scroll=1;else if(Keyboard.current.pageDownKey.isPressed)scroll=-1;}
            float distance=historyScroll.content.rect.height-historyScroll.viewport.rect.height;
            if(distance>0&&Mathf.Abs(scroll)>.15f)historyScroll.verticalNormalizedPosition=Mathf.Clamp01(historyScroll.verticalNormalizedPosition+scroll*500*Time.unscaledDeltaTime/distance);
        }
    }
}

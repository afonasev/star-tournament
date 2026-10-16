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
        ScrollRect historyScroll,historyVersionScroll;
        readonly List<Text> historyTextChunks=new List<Text>();
        readonly List<Button> historyButtons=new List<Button>();
        string historyShown,historySelected;
        Vector2 historyVersionViewportSize;
        NativeUpdateBridge.History historyData;
        void CreateReleaseHistoryUi(Transform parent)
        {
            historyAction=MenuButton(mainMenuScreen.transform,"release-history-action","Что нового",new Vector2(.61f,.072f),new Vector2(.78f,.13f),OpenReleaseHistory);
            historyScreen=Panel(parent,"release-history-screen",Vector2.zero,Vector2.one,MenuInk);AddMenuBackdrop(historyScreen,false);
            historyBack=MenuButton(historyScreen.transform,"release-history-back","‹ НАЗАД · B",new Vector2(.05f,.91f),new Vector2(.18f,.96f),CloseReleaseHistory);
            Label(historyScreen.transform,"release-history-heading","ЧТО НОВОГО",40,new Vector2(.21f,.89f),new Vector2(.94f,.96f),TextAnchor.MiddleLeft,MenuGold);
            historyStatus=Label(historyScreen.transform,"release-history-status","",18,new Vector2(.21f,.83f),new Vector2(.94f,.89f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            historyVersions=RosterScroll(historyScreen.transform,"release-history-versions",new Vector2(.05f,.15f),new Vector2(.29f,.81f),out historyVersionScroll).gameObject;
            AddHistoryScrollbar(historyVersionScroll.transform,"release-history-version-scrollbar",historyVersionScroll);
            var detail=Panel(historyScreen.transform,"release-history-detail",new Vector2(.31f,.15f),new Vector2(.95f,.81f),MenuCard);
            historyTitle=Label(detail.transform,"release-history-title","",28,new Vector2(.04f,.86f),new Vector2(.96f,.98f),TextAnchor.MiddleLeft,MenuGold);
            var content=RosterScroll(detail.transform,"release-history-scroll",new Vector2(.04f,.05f),new Vector2(.94f,.84f),out historyScroll);
            AddHistoryScrollbar(detail.transform,"release-history-scrollbar",historyScroll,new Vector2(.965f,.05f),new Vector2(.975f,.84f));
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
                var item=items[i];int index=i;
                var button=MenuButton(historyVersions.transform,"release-history-version-"+i,item.version+"\n"+HistoryDate(item.date),Vector2.zero,Vector2.one,()=>ShowReleaseNotes(index));
                button.GetComponent<MenuPresentation>().Compact=false;
                var rect=(RectTransform)button.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);
                rect.sizeDelta=new Vector2(-32,72);rect.anchoredPosition=new Vector2(0,-12-i*82);
                var text=button.GetComponentInChildren<Text>();text.fontSize=18;text.alignment=TextAnchor.MiddleLeft;
                if(item.installed){text.rectTransform.anchorMax=new Vector2(.60f,1);AddInstalledHistoryMark(button);}
                var reveal=button.gameObject.AddComponent<ReleaseHistoryRowReveal>();reveal.Reveal=()=>RevealHistoryVersion(index);
                historyButtons.Add(button);
            }
            ((RectTransform)historyVersions.transform).sizeDelta=new Vector2(0,count*82+24);
            historyVersionScroll.verticalNormalizedPosition=1;
            int chosen=count==0?-1:Array.FindIndex(items,0,count,x=>x.version==historySelected);
            if(chosen<0&&count>0)chosen=0;
            if(chosen>=0){ShowReleaseNotes(chosen);if(ownedFocus)Select(historyButtons[chosen]);}
            else{historyTitle.text="";historyBody.text="";}
        }
        void AddHistoryScrollbar(Transform parent,string name,ScrollRect scroll,Vector2? min=null,Vector2? max=null)
        {
            var track=Panel(parent,name,min??new Vector2(.97f,.025f),max??new Vector2(.985f,.975f),new Color32(48,68,83,255));
            var thumb=Panel(track.transform,"thumb",Vector2.zero,Vector2.one,MenuGold);
            var bar=track.AddComponent<Scrollbar>();bar.handleRect=(RectTransform)thumb.transform;bar.targetGraphic=thumb.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        }
        void AddInstalledHistoryMark(Button button)
        {
            var frame=new GameObject("history-installed-frame",typeof(RectTransform));frame.transform.SetParent(button.transform,false);
            Layout((RectTransform)frame.transform,Vector2.zero,Vector2.one);
            var anchors=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,1),new Vector2(0,0)};
            var ends=new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(1,1),new Vector2(1,0)};
            for(int edge=0;edge<4;edge++)
            {
                var go=new GameObject("edge-"+edge,typeof(RectTransform),typeof(Image));go.transform.SetParent(frame.transform,false);
                var rect=(RectTransform)go.transform;Layout(rect,anchors[edge],ends[edge]);
                rect.offsetMin=edge==1?new Vector2(-2,0):edge==2?new Vector2(0,-2):Vector2.zero;
                rect.offsetMax=edge==0?new Vector2(4,0):edge==3?new Vector2(0,2):Vector2.zero;
                var image=go.GetComponent<Image>();image.color=MenuGold;image.raycastTarget=false;
            }
            var badge=Panel(button.transform,"history-installed-badge",new Vector2(.63f,.57f),new Vector2(.97f,.93f),MenuGold);
            badge.GetComponent<Image>().raycastTarget=false;
            Label(badge.transform,"history-installed-label","УСТАНОВЛЕНА",14,Vector2.zero,Vector2.one,TextAnchor.MiddleCenter,MenuInk);
        }
        void RevealHistoryVersion(int index)
        {
            Canvas.ForceUpdateCanvases();historyVersionScroll.StopMovement();
            var content=historyVersionScroll.content;float offset=content.anchoredPosition.y,top=12+index*82,bottom=top+72,height=historyVersionScroll.viewport.rect.height;
            if(top<offset)offset=top;else if(bottom>offset+height)offset=bottom-height;
            content.anchoredPosition=new Vector2(0,Mathf.Clamp(offset,0,Mathf.Max(0,content.rect.height-height)));
        }
        static string HistoryDate(string value)=>DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date.ToString("dd.MM.yyyy",CultureInfo.InvariantCulture):value;
        void ShowReleaseNotes(int index)
        {
            var item=historyData.items[index];historySelected=item.version;RevealHistoryVersion(index);
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
                if(start+count<notes.Length){int newline=notes.LastIndexOf('\n',start+count-1,count);if(newline>=start)count=newline-start+1;else if(char.IsHighSurrogate(notes[start+count-1]))count--;}
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
            var size=historyVersionScroll.viewport.rect.size;
            if(size!=historyVersionViewportSize)
            {
                historyVersionViewportSize=size;
                var focused=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                int row=historyButtons.FindIndex(b=>b&&b.gameObject==focused);
                if(row>=0)RevealHistoryVersion(row);
            }
            var pad=menuDevice as Gamepad;
            bool back=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame||pad!=null&&pad.buttonEast.wasPressedThisFrame;
            if(back){CloseReleaseHistory();return;}
            float scroll=pad!=null?pad.rightStick.ReadValue().y:0;
            if(Keyboard.current!=null){if(Keyboard.current.pageUpKey.isPressed)scroll=1;else if(Keyboard.current.pageDownKey.isPressed)scroll=-1;}
            float distance=historyScroll.content.rect.height-historyScroll.viewport.rect.height;
            if(distance>0&&Mathf.Abs(scroll)>.15f)historyScroll.verticalNormalizedPosition=Mathf.Clamp01(historyScroll.verticalNormalizedPosition+scroll*500*Time.unscaledDeltaTime/distance);
        }
    }
    public sealed class ReleaseHistoryRowReveal : MonoBehaviour,ISelectHandler
    {
        public Action Reveal;
        public void OnSelect(BaseEventData data){Reveal?.Invoke();}
    }
}

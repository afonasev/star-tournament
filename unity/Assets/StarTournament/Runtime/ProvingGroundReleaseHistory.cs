using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
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
        readonly List<GameObject> historyMediaObjects=new List<GameObject>();
        readonly List<Texture2D> historyTextures=new List<Texture2D>();
        readonly List<Coroutine> historyImageLoads=new List<Coroutine>();
        int historyImageGeneration;
        string historyMediaRequested;
        Vector2 historyDetailSize;
        string historyShown,historySelected;
        Vector2 historyVersionViewportSize;
        NativeUpdateBridge.History historyData;
        void CreateReleaseHistoryUi(Transform parent)
        {
            historyAction=MenuButton(mainMenuScreen.transform,"release-history-action","Что нового",new Vector2(.61f,.072f),new Vector2(.78f,.13f),OpenReleaseHistory);
            historyScreen=Panel(parent,"release-history-screen",Vector2.zero,Vector2.one,MenuInk);AddMenuBackdrop(historyScreen,false);
            historyBack=MenuButton(historyScreen.transform,"release-history-back","‹ НАЗАД",new Vector2(.05f,.91f),new Vector2(.18f,.96f),CloseReleaseHistory);
            Label(historyScreen.transform,"release-history-heading","ЧТО НОВОГО",40,new Vector2(.21f,.89f),new Vector2(.94f,.96f),TextAnchor.MiddleLeft,MenuGold);
            historyStatus=Label(historyScreen.transform,"release-history-status","",18,new Vector2(.21f,.83f),new Vector2(.94f,.89f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            BindMenuShortcut(historyBack,MenuPadButton.East,()=>phase==Phase.History);
            historyVersions=RosterScroll(historyScreen.transform,"release-history-versions",new Vector2(.05f,.15f),new Vector2(.29f,.81f),out historyVersionScroll).gameObject;
            AddHistoryScrollbar(historyVersionScroll.transform,"release-history-version-scrollbar",historyVersionScroll);
            var detail=Panel(historyScreen.transform,"release-history-detail",new Vector2(.31f,.15f),new Vector2(.95f,.81f),MenuCard);
            historyTitle=Label(detail.transform,"release-history-title","",28,new Vector2(.04f,.86f),new Vector2(.96f,.98f),TextAnchor.MiddleLeft,MenuGold);
            var content=RosterScroll(detail.transform,"release-history-scroll",new Vector2(.04f,.05f),new Vector2(.94f,.84f),out historyScroll);
            AddHistoryScrollbar(detail.transform,"release-history-scrollbar",historyScroll,new Vector2(.965f,.05f),new Vector2(.975f,.84f));
            historyBody=Label(content,"release-history-description","",22,Vector2.zero,Vector2.one,TextAnchor.UpperLeft,Color.white);
            historyBody.supportRichText=false;historyBody.raycastTarget=false;
            historyBody.rectTransform.anchorMin=new Vector2(0,1);historyBody.rectTransform.anchorMax=Vector2.one;historyBody.rectTransform.pivot=new Vector2(.5f,1);
            var controls=Label(historyScreen.transform,"release-history-controls","Крестовина: версия · Правый стик: прокрутка",18,new Vector2(.05f,.07f),new Vector2(.95f,.12f),TextAnchor.MiddleLeft,new Color32(153,173,187,255));
            controls.gameObject.AddComponent<MenuPadOnlyHint>().Show=()=>menuGamepadCursor&&menuWindowFocused&&!textEntryPanel;
            historyScreen.SetActive(false);
        }
        void OpenReleaseHistory()
        {
            if(phase!=Phase.MainMenu)return;
            phase=Phase.History;historyShown=null;historyMediaRequested=null;RefreshInterface();Select(historyBack);
            updateBridge?.RequestHistory();RefreshReleaseHistory();
        }
        void CloseReleaseHistory()
        {updateBridge?.CloseHistory();ClearHistoryImages();historyMediaRequested=null;phase=Phase.MainMenu;RefreshInterface();Select(historyAction);}
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
            else{ClearHistoryImages();historyTitle.text="";historyBody.text="";}
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
            button.GetComponent<MenuActionHint>().ReservedRightInset=()=>((RectTransform)button.transform).rect.xMax-RectTransformUtility.CalculateRelativeRectTransformBounds(button.transform,(RectTransform)badge.transform).min.x+4;
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
        void ClearHistoryImages()
        {
            historyImageGeneration++;
            foreach(var load in historyImageLoads)if(load!=null)StopCoroutine(load);historyImageLoads.Clear();
            foreach(var go in historyMediaObjects)if(go){go.SetActive(false);Destroy(go);}historyMediaObjects.Clear();
            foreach(var texture in historyTextures)if(texture)Destroy(texture);historyTextures.Clear();
        }
        IEnumerator LoadHistoryImage(NativeUpdateBridge.HistoryBlock block,RawImage image,Text status,int generation)
        {
            // Disk IO and digest verification stay off the render thread; Unity owns bounded decode.
            var read=Task.Run(()=>
            {
                try
                {
                    var info=new FileInfo(block.path);
                    if(!info.Exists||info.Length>2*1024*1024)return null;
                    byte[] bytes;
                    using(var stream=File.OpenRead(block.path))
                    {
                        if(stream.Length>2*1024*1024)return null;
                        bytes=new byte[(int)stream.Length];int offset=0;
                        while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count==0)return null;offset+=count;}
                        if(stream.ReadByte()!=-1)return null;
                    }
                    using(var sha=SHA256.Create())
                    {if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!=block.sha256)return null;}
                    if(bytes.Length<29||bytes[0]!=137||bytes[1]!=80||bytes[2]!=78||bytes[3]!=71)return null;
                    int width=(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19];
                    int height=(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23];
                    return width==block.width&&height==block.height?bytes:null;
                }
                catch(Exception ex) when(ex is IOException||ex is UnauthorizedAccessException||ex is ArgumentException){return null;}
            });
            while(!read.IsCompleted)yield return null;
            if(generation!=historyImageGeneration||!image||phase!=Phase.History)yield break;
            if(read.IsFaulted||read.Result==null){status.text="Изображение недоступно";yield break;}
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(texture,read.Result,true)||texture.width!=block.width||texture.height!=block.height)
            {Destroy(texture);status.text="Изображение недоступно";yield break;}
            historyTextures.Add(texture);image.texture=texture;image.color=Color.white;status.text="";
        }
        void ShowReleaseNotes(int index)
        {
            var item=historyData.items[index];bool same=historySelected==item.version;
            float previousScroll=historyScroll.verticalNormalizedPosition;
            historySelected=item.version;RevealHistoryVersion(index);
            historyTitle.text="Версия "+item.version+" · "+HistoryDate(item.date)+(item.installed?" · Установлена":"");
            if(historyMediaRequested!=item.version&&((item.russianBlocks!=null&&Array.Exists(item.russianBlocks,x=>x!=null&&x.kind=="image"))||(item.russian??"").Contains("![")))
            {historyMediaRequested=item.version;updateBridge?.RequestHistoryMedia(item.version);}
            string notes=string.IsNullOrWhiteSpace(item.russian)?"Описание на русском языке не опубликовано для этой версии.":item.russian;
            ClearHistoryImages();
            foreach(var chunk in historyTextChunks)if(chunk){chunk.gameObject.SetActive(false);Destroy(chunk.gameObject);}historyTextChunks.Clear();
            historyBody.text="";Canvas.ForceUpdateCanvases();
            float height=0;int part=0;float width=historyScroll.viewport.rect.width;
            void AddText(string value)
            {
                int start=0;
                while(start<value.Length)
                {
                    int count=Math.Min(8000,value.Length-start);
                    if(start+count<value.Length){int newline=value.LastIndexOf('\n',start+count-1,count);if(newline>=start)count=newline-start+1;else if(char.IsHighSurrogate(value[start+count-1]))count--;}
                    var text=part==0?historyBody:Label(historyScroll.content,"release-history-description-"+part,"",22,Vector2.zero,Vector2.one,TextAnchor.UpperLeft,Color.white);
                    if(part>0)historyTextChunks.Add(text);
                    text.supportRichText=false;text.raycastTarget=false;text.text=value.Substring(start,count);
                    var rect=text.rectTransform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);
                    rect.sizeDelta=Vector2.zero;rect.anchoredPosition=new Vector2(0,-height);
                    Canvas.ForceUpdateCanvases();float chunkHeight=text.preferredHeight+4;rect.sizeDelta=new Vector2(0,chunkHeight);
                    height+=chunkHeight;start+=count;part++;
                }
            }
            var blocks=item.russianBlocks;
            if(blocks==null||blocks.Length==0||string.IsNullOrWhiteSpace(item.russian))AddText(notes);
            else foreach(var block in blocks)
            {
                if(block==null)continue;
                if(block.kind!="image")
                {
                    // Launcher references slices rather than duplicating up to eleven long notes in IPC.
                    string value=block.text??"";
                    if(block.length>0&&block.offset>=0&&block.offset<=notes.Length-block.length)
                        value=Regex.Replace(Regex.Replace(notes.Substring(block.offset,block.length).Trim(),@"(?m)^### +",""),@"(?m)^- +","• ");
                    AddText(value);height+=12;continue;
                }
                bool ready=block.state=="ready"&&block.width>0&&block.width<=1600&&block.height>0&&block.height<=1200&&!string.IsNullOrEmpty(block.path);
                float imageHeight=ready?width*block.height/block.width:72;
                var go=new GameObject("release-history-image",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(historyScroll.content,false);historyMediaObjects.Add(go);
                var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,imageHeight);rect.anchoredPosition=new Vector2(0,-height);
                var image=go.GetComponent<RawImage>();image.color=new Color32(24,41,55,255);image.raycastTarget=false;
                var status=Label(go.transform,"release-history-image-state",ready?"Загрузка изображения…":block.state=="unavailable"?"Изображение недоступно":"Загрузка изображения…",18,Vector2.zero,Vector2.one,TextAnchor.MiddleCenter,new Color32(153,173,187,255));status.raycastTarget=false;
                if(ready)historyImageLoads.Add(StartCoroutine(LoadHistoryImage(block,image,status,historyImageGeneration)));
                height+=imageHeight+8;AddText(block.text??"");height+=20;
            }
            historyScroll.content.sizeDelta=new Vector2(0,Mathf.Max(historyScroll.viewport.rect.height,height+20));
            historyScroll.verticalNormalizedPosition=same?previousScroll:1;historyDetailSize=historyScroll.viewport.rect.size;
        }
        void UpdateReleaseHistory()
        {
            if(phase!=Phase.History)return;RefreshReleaseHistory();
            if(historyDetailSize!=historyScroll.viewport.rect.size&&historyData?.items!=null)
            {int selected=Array.FindIndex(historyData.items,x=>x.version==historySelected);if(selected>=0)ShowReleaseNotes(selected);}
            var size=historyVersionScroll.viewport.rect.size;
            if(size!=historyVersionViewportSize)
            {
                historyVersionViewportSize=size;
                var focused=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                int row=historyButtons.FindIndex(b=>b&&b.gameObject==focused);
                if(row>=0)RevealHistoryVersion(row);
            }
            var pad=menuDevice as Gamepad;
            if(TryTakeMenuBack()){if(historyBack.IsInteractable())historyBack.onClick.Invoke();return;}
            float scroll=pad!=null?pad.rightStick.ReadValue().y:0;
            if(Keyboard.current!=null){if(Keyboard.current.pageUpKey.isPressed)scroll=1;else if(Keyboard.current.pageDownKey.isPressed)scroll=-1;}
            float distance=historyScroll.content.rect.height-historyScroll.viewport.rect.height;
            if(distance>0&&Mathf.Abs(scroll)>MenuHistoryScrollDeadzone)historyScroll.verticalNormalizedPosition=Mathf.Clamp01(historyScroll.verticalNormalizedPosition+scroll*500*Time.unscaledDeltaTime/distance);
        }
    }
    public sealed class ReleaseHistoryRowReveal : MonoBehaviour,ISelectHandler
    {
        public Action Reveal;
        public void OnSelect(BaseEventData data){Reveal?.Invoke();}
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Presentation-only IPC. Networking, trust and activation belong to the launcher.
    public sealed class NativeUpdateBridge : MonoBehaviour
    {
        [Serializable] public sealed class Snapshot
        {
            public string state,version,matchRequest;
            public int progress;
            public string error;
            public bool restartAcknowledged,matchPaused;
            public History history;
        }
        [Serializable] public sealed class HistoryItem { public string version,date,russian,english; public bool installed; }
        [Serializable] public sealed class History { public string state,error; public HistoryItem[] items; }
        public History HistoryState {get{ReadSnapshot();return shown?.history;}}
        public void RequestHistory(){if(enabled)QueueCommand("history");}
        public sealed class View
        {
            public string Message, Button, Action;
            public bool Enabled;
            public View(string message,string button,string action,bool enabled)
            {Message=message;Button=button;Action=action;Enabled=enabled;}
        }
        public static View Describe(Snapshot state)
        {
            string version=string.IsNullOrEmpty(state?.version)?"Обновление":"Версия "+state.version;
            switch(state?.state)
            {
                case "available": return new View(string.IsNullOrEmpty(state.version)?"Доступно обновление": "Доступна версия "+state.version,"Обновить","download",true);
                case "downloading": return new View(version+" · загрузка "+Math.Max(0,Math.Min(100,state.progress))+"%\nПри начале матча загрузка приостановится и продолжится в меню.","Отменить","cancel",true);
                case "verifying": return new View(version+" · проверка пакета…","Отменить","cancel",true);
                case "paused": return new View(version+" · загрузка приостановлена на время матча","","",false);
                case "cancelling": return new View("Отмена загрузки…","","",false);
                case "staged": return new View((string.IsNullOrEmpty(state.version)?"Обновление готово.":version+" готова.")+" Установится после выхода из игры. Можно продолжить играть.","Перезапустить","restart",true);
                case "current": return new View("","","",false);
                case "unavailable": return new View((string.IsNullOrEmpty(state.error)?"Не удалось проверить обновления. Проверьте подключение.":state.error)+" Следующая проверка — при следующем запуске.","","",false);
                case "error": return new View(string.IsNullOrEmpty(state.error)?"Не удалось скачать обновление. Повторите попытку.":state.error,string.IsNullOrEmpty(state.version)?"":"Повторить",string.IsNullOrEmpty(state.version)?"":"download",!string.IsNullOrEmpty(state.version));
                case "apply-error": return new View(string.IsNullOrEmpty(state.error)?"Предыдущая установка не завершилась. Повторите установку.":state.error,"Повторить установку","restart",true);
                default: return new View("","","",false);
            }
        }
        Text message,buttonText;
        Image progressTrack,progressFill;
        Button button;
        string statusPath,commandPath,command;
        volatile string latest;
        volatile bool writing;
        bool writerRunning;
        volatile bool restartRequested;
        string previous,matchRequest;
        float matchDeadline;
        Snapshot shown;
        volatile string commandError;
        CancellationTokenSource cancellation;
        readonly object commandGate=new object();
        readonly Queue<string> commands=new Queue<string>();
        public void Bind(Text status,Button action,Image track=null,Image fill=null)
        {
            message=status;button=action;buttonText=action.GetComponentInChildren<Text>();
            progressTrack=track;progressFill=fill;if(progressTrack)progressTrack.gameObject.SetActive(false);
            button.gameObject.SetActive(false);message.gameObject.SetActive(false);
            statusPath=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS");
            commandPath=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND");
            if(string.IsNullOrEmpty(statusPath)||string.IsNullOrEmpty(commandPath)||!Path.IsPathFullyQualified(statusPath)||!Path.IsPathFullyQualified(commandPath))
            {enabled=false;return;}
            button.onClick.AddListener(Activate);
            cancellation=new CancellationTokenSource();var token=cancellation.Token;
            _=Task.Run(async()=>
            {
                while(!token.IsCancellationRequested)
                {
                    try {if(new FileInfo(statusPath).Length<=8*1024*1024)latest=File.ReadAllText(statusPath);}
                    catch(IOException) { } catch(UnauthorizedAccessException) { }
                    try {await Task.Delay(100,token).ConfigureAwait(false);} catch(OperationCanceledException) {break;}
                }
            },token);
        }
        void Activate()
        {
            if(writing||string.IsNullOrEmpty(command)||!button.interactable)return;
            commandError=null;if(command=="restart")restartRequested=true;
            QueueCommand(command);
        }
        void QueueCommand(string action,string id="")
        {
            if(cancellation==null||cancellation.IsCancellationRequested)return;
            lock(commandGate)
            {
                commands.Enqueue("{\"action\":\""+action+"\",\"id\":\""+id+"\"}");writing=true;
                if(writerRunning)return;writerRunning=true;
            }
            var token=cancellation.Token;
            _=Task.Run(async()=>
            {
                try
                {
                    while(!token.IsCancellationRequested)
                    {
                        string payload;
                        lock(commandGate)
                        {if(commands.Count==0){writing=writerRunning=false;return;}payload=commands.Peek();}
                        // Preserve the previous command until the launcher consumes it.
                        while(File.Exists(commandPath))await Task.Delay(25,token).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        var next=commandPath+".unity-next";File.WriteAllText(next,payload);
                        token.ThrowIfCancellationRequested();File.Move(next,commandPath);
                        lock(commandGate)commands.Dequeue();
                    }
                }
                catch(OperationCanceledException) when(token.IsCancellationRequested) { }
                catch(IOException) {restartRequested=false;commandError="Не удалось передать команду обновления. Проверьте свободное место и повторите попытку.";}
                catch(UnauthorizedAccessException) {restartRequested=false;commandError="Нет доступа к файлу команды обновления. Проверьте права и повторите попытку.";}
                lock(commandGate){commands.Clear();writing=writerRunning=false;}
                try{File.Delete(commandPath+".unity-next");}catch(IOException){}catch(UnauthorizedAccessException){}
            },token);
        }
        public void PauseBeforeMatch()
        {
            if(!enabled||matchRequest!=null)return;
            commandError=null;matchRequest=Guid.NewGuid().ToString("N");matchDeadline=Time.realtimeSinceStartup+10;
            QueueCommand("match",matchRequest);
        }
        public bool IsWaitingForMatchPause {get;private set;}
        public bool MatchReady {get{ReadSnapshot();return !enabled||matchRequest==null||shown!=null&&shown.matchPaused&&shown.matchRequest==matchRequest;}}
        public IEnumerator WaitForMatchPause()
        {
            IsWaitingForMatchPause=true;
            try
            {
                while(!MatchReady)
                {
                    if(!string.IsNullOrEmpty(commandError))throw new InvalidOperationException(commandError);
                    if(Time.realtimeSinceStartup>=matchDeadline)throw new InvalidOperationException("Не удалось подтвердить остановку обновления. Повторите запуск матча.");
                    yield return null;
                }
            }
            finally{IsWaitingForMatchPause=false;}
        }
        public void ReturnToMenu()
        {if(matchRequest==null)return;QueueCommand("menu",matchRequest);matchRequest=null;}
        void ReadSnapshot()
        {
            var data=latest;if(string.IsNullOrEmpty(data)||data==previous)return;
            try{shown=JsonUtility.FromJson<Snapshot>(data);}catch(ArgumentException){return;}previous=data;
        }
        void Update()
        {
            ReadSnapshot();if(shown==null)return;
            if(progressTrack)progressTrack.gameObject.SetActive(shown.state=="downloading"||shown.state=="verifying"||shown.state=="paused");
            if(progressFill)progressFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(shown.progress/100f),1);
            var view=Describe(shown);if(!string.IsNullOrEmpty(commandError))view.Message=commandError;
            message.color=shown.state=="error"||shown.state=="apply-error"||shown.state=="unavailable"||!string.IsNullOrEmpty(commandError)?new Color32(255,218,184,255):new Color32(153,173,187,255);
            if(message.text!=view.Message)message.text=view.Message;message.gameObject.SetActive(!string.IsNullOrEmpty(view.Message));
            if(buttonText.text!=view.Button)buttonText.text=view.Button;command=view.Action;
            button.gameObject.SetActive(!string.IsNullOrEmpty(view.Button));button.interactable=view.Enabled&&!writing;
            if(restartRequested&&shown.restartAcknowledged){restartRequested=false;Application.Quit();}
        }
        void OnDestroy()
        {if(button)button.onClick.RemoveListener(Activate);cancellation?.Cancel();cancellation?.Dispose();}
    }
}

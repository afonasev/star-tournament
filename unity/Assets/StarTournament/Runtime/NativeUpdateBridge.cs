using System;
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
            public string state;
            public int progress;
            public string error;
            public bool restartAcknowledged;
        }
        public sealed class View
        {
            public string Message, Button, Action;
            public bool Enabled;
            public View(string message,string button,string action,bool enabled)
            {Message=message;Button=button;Action=action;Enabled=enabled;}
        }
        public static View Describe(Snapshot state)
        {
            switch(state?.state)
            {
                case "available": return new View("","Обновить","download",true);
                case "downloading": return new View(state.progress>=100?"Проверка обновления…":"Загрузка · "+Math.Max(0,Math.Min(100,state.progress))+"%","Обновить","",false);
                case "staged": return new View("","Перезапустить","restart",true);
                case "current": return new View("","","",false);
                case "unavailable": return new View(string.IsNullOrEmpty(state.error)?"Не удалось проверить обновления. Проверьте подключение.":state.error,"Повторить проверку","check",true);
                case "error": return new View(string.IsNullOrEmpty(state.error)?"Не удалось скачать обновление. Повторите попытку.":state.error,"Повторить","download",true);
                case "apply-error": return new View(string.IsNullOrEmpty(state.error)?"Предыдущая установка не завершилась. Повторите установку.":state.error,"Повторить установку","restart",true);
                default: return new View("","","",false);
            }
        }
        Text message,buttonText;
        Button button;
        string statusPath,commandPath,command;
        volatile string latest;
        volatile bool writing;
        volatile bool restartRequested;
        string previous;
        Snapshot shown;
        volatile string commandError;
        CancellationTokenSource cancellation;

        public void Bind(Text status,Button action)
        {
            message=status;button=action;buttonText=action.GetComponentInChildren<Text>();
            button.gameObject.SetActive(false);
            message.gameObject.SetActive(false);
            statusPath=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_STATUS");
            commandPath=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_UPDATE_COMMAND");
            if(string.IsNullOrEmpty(statusPath)||string.IsNullOrEmpty(commandPath)||
                !Path.IsPathFullyQualified(statusPath)||!Path.IsPathFullyQualified(commandPath))
            { message.gameObject.SetActive(false);button.gameObject.SetActive(false);enabled=false;return; }
            button.onClick.AddListener(Activate);
            cancellation=new CancellationTokenSource();var token=cancellation.Token;
            _=Task.Run(async()=>
            {
                while(!token.IsCancellationRequested)
                {
                    try
                    {
                        if(new FileInfo(statusPath).Length<=65536)latest=File.ReadAllText(statusPath);
                    }
                    catch(IOException) { }
                    catch(UnauthorizedAccessException) { }
                    try { await Task.Delay(100,token).ConfigureAwait(false); }
                    catch(OperationCanceledException) { break; }
                }
            },token);
        }
        void Activate()
        {
            if(writing||string.IsNullOrEmpty(command)||!button.interactable)return;
            commandError=null;
            var action=command;writing=true;button.interactable=false;
            if(action=="restart")restartRequested=true;
            _=Task.Run(()=>
            {
                try
                {
                    var next=commandPath+".unity-next";
                    File.WriteAllText(next,"{\"action\":\""+action+"\"}");
                    // Launcher consumes only the completed command file.
                    if(File.Exists(commandPath))File.Delete(commandPath);
                    File.Move(next,commandPath);
                }
                catch(IOException) { restartRequested=false;commandError="Не удалось передать команду обновления. Проверьте свободное место и повторите попытку."; }
                catch(UnauthorizedAccessException) { restartRequested=false;commandError="Нет доступа к файлу команды обновления. Проверьте права и повторите попытку."; }
                finally { writing=false; }
            });
        }
        void Update()
        {
            var data=latest;
            if(string.IsNullOrEmpty(data))return;
            if(data!=previous)
            {
                try {shown=JsonUtility.FromJson<Snapshot>(data);}
                catch(ArgumentException) {return;}
                previous=data;
            }
            if(shown==null)return;
            var view=Describe(shown);
            if(!string.IsNullOrEmpty(commandError))view.Message=commandError;
            message.color=shown.state=="error"||shown.state=="apply-error"||shown.state=="unavailable"||!string.IsNullOrEmpty(commandError)
                ?new Color32(255,218,184,255):new Color32(153,173,187,255);
            if(message.text!=view.Message)message.text=view.Message;
            message.gameObject.SetActive(!string.IsNullOrEmpty(view.Message));
            if(buttonText.text!=view.Button)buttonText.text=view.Button;
            command=view.Action;
            button.gameObject.SetActive(!string.IsNullOrEmpty(view.Button));
            button.interactable=view.Enabled&&!writing;
            if(restartRequested&&shown.restartAcknowledged)
            {
                restartRequested=false;Application.Quit(); // Lab may still cancel a normal quit.
            }
        }
        void OnDestroy()
        {
            if(button)button.onClick.RemoveListener(Activate);
            cancellation?.Cancel();cancellation?.Dispose();
        }
    }
}

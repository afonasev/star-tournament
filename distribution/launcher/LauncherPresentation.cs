using System.Net;
using System.Text;

namespace StarTournament.Distribution;

public enum LauncherAction { Play, Update, Retry, Cancel, Quit, Notes }
public sealed record LauncherView(long Id, long Revision, BootstrapProgressStatus Status,
    LauncherAction[] Actions, bool Notes = false, bool CanClose = false);
public sealed record LauncherBox(int X, int Y, int Width, int Height);
public sealed record LauncherLabel(string Key, LauncherBox Box, string Text, int Size, string Color, bool Bold = false);
public sealed record LauncherButton(LauncherAction Action, LauncherBox Box, string Text, bool Primary = false, bool Link = false, int FontSize = 14);
public sealed record LauncherFrame(LauncherLabel[] Labels, LauncherButton[] Buttons, LauncherBox Progress, bool ShowProgress, bool Marquee, int Percent, LauncherBox Logo, LauncherBox HeaderLine);

/// <summary>Owns consent and consumes each decision once, independently of a native HWND.</summary>
public sealed class LauncherSurface
{
    readonly object gate = new();
    LauncherView view = new(1, 1, new("checking"), [LauncherAction.Quit], CanClose: true);
    TaskCompletionSource<LauncherAction>? pending;
    bool closed;
    public LauncherView View { get { lock (gate) return view; } }
    public void Publish(BootstrapProgressStatus status)
    {
        lock (gate)
        {
            if (closed || view.Status.State == "cancelling" && status.State == "downloading") return;
            if (view.Status == status) return;
            var actions = status.State switch { "checking" => new[] { LauncherAction.Quit }, "downloading" => [LauncherAction.Cancel], _ => Array.Empty<LauncherAction>() };
            bool same = view.Status.State == status.State && view.Status.Version == status.Version && view.Actions.SequenceEqual(actions);
            view = new(same ? view.Id : view.Id + 1, view.Revision + 1, status, actions, CanClose: status.State is "checking" or "downloading");
        }
    }
    public Task<LauncherAction> Decision(BootstrapProgressStatus status, bool retry = false, bool playable = true)
    {
        lock (gate)
        {
            if (closed) return Task.FromResult(LauncherAction.Quit);
            if (pending != null) throw new InvalidOperationException("A launcher decision is already pending");
            pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            bool offer = status.State == "available";
            view = new(view.Id + 1, view.Revision + 1, status,
                !playable ? [LauncherAction.Quit] : offer ? [LauncherAction.Play, LauncherAction.Update] : retry ? [LauncherAction.Play, LauncherAction.Retry] : [LauncherAction.Play, LauncherAction.Quit],
                Notes: offer && HasNotes(status.Version), CanClose: true);
            return pending.Task;
        }
    }
    static bool HasNotes(string version)
    {try{BootstrapChangelog.ReleaseUri(version);return true;}catch(ArgumentException){return false;}}
    public bool Choose(long id, LauncherAction action)
    {
        lock (gate)
        {
            if (closed || id != view.Id) return false;
            if (action == LauncherAction.Notes) return view.Notes;
            if (!view.Actions.Contains(action) && !(action == LauncherAction.Quit && view.CanClose)) return false;
            var waiter = pending; pending = null;
            view = new(view.Id + 1, view.Revision + 1,
                action == LauncherAction.Cancel ? view.Status with { State = "cancelling" } : view.Status,
                [], CanClose: false);
            waiter?.TrySetResult(action);
            return true;
        }
    }
    public bool TryBeginApply()
    {
        lock (gate)
        {
            if (closed || view.Status.State != "downloading") return false;
            view = new(view.Id + 1, view.Revision + 1, view.Status with { State = "applying", Progress = 100 }, []);
            return true;
        }
    }
    public LauncherAction? CloseAction => View.Status.State == "downloading" ? LauncherAction.Cancel : View.CanClose ? LauncherAction.Quit : null;
    public void Close()
    {
        lock (gate) { closed = true; view = view with { Actions = [], CanClose = false, Notes = false }; pending?.TrySetResult(LauncherAction.Quit); pending = null; }
    }
}

public static class LauncherPresentation
{
    public const string Background = "#0B1622", Foreground = "#F5F7FA", Muted = "#A9BECD", Accent = "#E5BD72", Button = "#233B4D", Border = "#506878";
    public static string ActionText(LauncherAction action) => action switch {
        LauncherAction.Play => "Играть сейчас", LauncherAction.Update => "Обновить и запустить", LauncherAction.Retry => "Повторить",
        LauncherAction.Cancel => "Отменить", LauncherAction.Quit => "Закрыть", _ => BootstrapChangelog.Label
    };
    public static LauncherFrame Frame(LauncherView view, int width = 620, int height = 390)
    {
        int available = Math.Max(280, width - 52);
        var status = view.Status;
        var content = status.State switch {
            "available" => ("Обновить Star Tournament?", "Доступна новая версия. После обновления игра запустится автоматически."),
            "downloading" => ("Загружаем обновление", "Скачиваем и проверяем пакет. Когда он будет готов, игра откроется сама."),
            "cancelling" => ("Отменяем обновление", "Останавливаем загрузку перед запуском установленной игры."),
            "applying" => ("Устанавливаем обновление", "Подождите, применяем проверенное обновление. Подтвердите запрос Windows, если он появится."),
            "launching" => ("Запускаем игру", "Пожалуйста, дождитесь появления окна игры."),
            "blocked" => ("Не удалось запустить игру", status.Error),
            "error" or "apply-error" or "unavailable" => ("Обновление прервалось", status.Error),
            _ => ("Проверяем обновления", "Проверяем наличие новой версии. Пожалуйста, подождите.")
        };
        bool error = status.State is "error" or "apply-error" or "unavailable" or "blocked";
        bool bar = status.State is "downloading" or "cancelling" or "checking" or "applying" or "launching";
        var labels = new List<LauncherLabel> {
            new("brand", new(106,28,available-80,28), "STAR",22,Foreground,true),
            new("brand-subtitle", new(106,57,available-80,24), "TOURNAMENT",18,Accent,true),
            new("title", new(26,126,available,42), content.Item1, width < 460 ? 23 : 26, Foreground,true),
            new("description",new(26,177,available,view.Notes ? 50 : error ? 62 : 54),content.Item2,15,Muted),
            new("hint",new(26,height-98,available,23),view.Actions.Length>0 ? "Клавиатура или контроллер · выбор и подтверждение" : "",12,Muted)
        };
        if (error) labels.Add(new("journal",new(26,248,available,Math.Max(28,height-352)),"Журнал: " + UpdateFeedback.LogPath,11,Muted));
        if (status.Version.Length > 0 && status.State == "available") labels.Add(new("version",new(26,266,available,21),"Новая версия: " + status.Version,12,Muted));
        if (bar && status.State is "downloading" or "cancelling") labels.Add(new("percent",new(26,263,available,21),Math.Clamp(status.Progress,0,100)+"%",13,Muted));
        var buttons = new List<LauncherButton>();
        if (view.Notes) buttons.Add(new(LauncherAction.Notes,new(26,230,Math.Min(250,available),30),BootstrapChangelog.Label,Link:true));
        var widths = view.Actions.Select(a => a == LauncherAction.Update ? 204 : a == LauncherAction.Play ? 144 : 116).ToArray();
        int sum = widths.Sum() + Math.Max(0,widths.Length-1)*12;
        if (sum > available && widths.Length > 0) { int each=(available-12*(widths.Length-1))/widths.Length; Array.Fill(widths,each); sum=available; }
        int x = width-26-sum;
        for(int i=0;i<view.Actions.Length;i++) { var action=view.Actions[i];buttons.Add(new(action,new(x,height-70,widths[i],44),ActionText(action),action is LauncherAction.Update or LauncherAction.Retry,FontSize:widths[i]<170?13:14)); x+=widths[i]+12; }
        return new(labels.ToArray(),buttons.ToArray(),new(26,246,available,8),bar,status.State != "downloading",Math.Clamp(status.Progress,0,100),new(26,22,64,64),new(26,104,available,1));
    }
    // A source-derived preview. Native Win32 controls use the same boxes/text/palette;
    // HTML rendering on another OS is explicitly not evidence of native Windows pixels.
    public static string Html(IEnumerable<LauncherView> views)
    {
        var html=new StringBuilder("<!doctype html><meta charset='utf-8'><style>body{background:#dfe4e9;margin:8px;font-family:'Segoe UI',Arial,sans-serif;display:flex;flex-wrap:wrap;gap:8px}.window{position:relative;width:620px;height:390px;background:"+Background+";border:1px solid "+Border+";border-radius:8px;overflow:hidden}p,button{position:absolute;margin:0;box-sizing:border-box}p{white-space:pre-wrap;overflow:hidden;line-height:1.3}button{border:1px solid "+Border+";border-radius:8px;background:"+Button+";color:"+Foreground+";font:600 14px 'Segoe UI',Arial,sans-serif;padding:0 10px}.primary{background:"+Accent+";border-color:"+Accent+";color:"+Background+"}.link{border:0;background:transparent;color:"+Accent+";text-decoration:underline;text-align:left;padding:0}.bar{position:absolute;border-radius:6px;background:"+Button+";overflow:hidden}.bar span{display:block;height:100%;background:"+Accent+"}</style>");
        static string Box(LauncherBox r)=>$"left:{r.X}px;top:{r.Y}px;width:{r.Width}px;height:{r.Height}px;";
        foreach(var view in views) {
            var frame=Frame(view);html.Append("<section class='window' data-state='").Append(WebUtility.HtmlEncode(view.Status.State)).Append("'>");
            html.Append("<img alt='Логотип Star Tournament' src='").Append(LauncherBrand.DataUri).Append("' style='position:absolute;").Append(Box(frame.Logo)).Append("'>");
            html.Append("<div style='position:absolute;background:"+Button+";").Append(Box(frame.HeaderLine)).Append("'></div>");
            foreach(var label in frame.Labels) html.Append("<p style='").Append(Box(label.Box)).Append("font-size:").Append(label.Size).Append("px;color:").Append(label.Color).Append(";font-weight:").Append(label.Bold?"650":"400").Append("'>").Append(WebUtility.HtmlEncode(label.Text)).Append("</p>");
            if(frame.ShowProgress) html.Append("<div class='bar' style='").Append(Box(frame.Progress)).Append("'><span style='width:").Append(frame.Marquee?24:frame.Percent).Append("%'></span></div>");
            foreach(var button in frame.Buttons) html.Append("<button class='").Append(button.Link?"link":button.Primary?"primary":"").Append("' style='").Append(Box(button.Box)).Append("font-size:").Append(button.FontSize).Append("px;'>").Append(WebUtility.HtmlEncode(button.Text)).Append("</button>");
            html.Append("</section>");
        }
        return html.ToString();
    }
}

/// <summary>Rising edges only; reconnect/focus/held buttons never grant consent.</summary>
public sealed class LauncherPadEdges
{
    bool seeded; ushort previous; int direction;
    public (int Move,bool Confirm,bool Back) Read(bool connected,bool focused,ushort buttons,short x=0,short y=0)
    {
        if(!connected||!focused){seeded=false;return default;}
        int next=(buttons&0x0005)!=0||x < -19660||y > 19660 ? -1 : (buttons&0x000a)!=0||x > 19660||y < -19660 ? 1 : 0;
        if(!seeded){seeded=true;previous=buttons;direction=next;return default;}
        var result=(next!=0&&next!=direction?next:0,(buttons&0x1000)!=0&&(previous&0x1000)==0,(buttons&0x2000)!=0&&(previous&0x2000)==0);
        previous=buttons;direction=next;return result;
    }
}

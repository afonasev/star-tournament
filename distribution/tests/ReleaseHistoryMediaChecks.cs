using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using StarTournament.Distribution;

static class ReleaseHistoryMediaChecks
{
    // Real PNG encoder for bounded fixtures; the implementation under test is the launcher source.
    static byte[] Png(int width = 2, int height = 1)
    {
        using var output = new MemoryStream(); output.Write(new byte[]{137,80,78,71,13,10,26,10});
        void Chunk(string kind, byte[] data)
        {
            byte[] length=new byte[4];BinaryPrimitives.WriteInt32BigEndian(length,data.Length);output.Write(length);
            byte[] name=System.Text.Encoding.ASCII.GetBytes(kind);output.Write(name);output.Write(data);
            uint crc=0xffffffff;foreach(byte value in name.Concat(data)){crc^=value;for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}
            BinaryPrimitives.WriteUInt32BigEndian(length,~crc);output.Write(length);
        }
        byte[] header=new byte[13];BinaryPrimitives.WriteInt32BigEndian(header,width);BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),height);header[8]=8;header[9]=2;Chunk("IHDR",header);
        using var compressed = new MemoryStream();using(var zip=new ZLibStream(compressed,CompressionMode.Compress,true))zip.Write(new byte[]{0,255,0,0,0,255,0});
        Chunk("IDAT",compressed.ToArray());Chunk("IEND",Array.Empty<byte>());return output.ToArray();
    }
    public static async Task Run()
    {
        int count=0;void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}
        byte[] data=Png();string sha=Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        string url="https://github.com/afonasev/star-tournament/releases/download/v1.0.0/history-"+sha+".png";
        var blocks=ReleaseHistoryMedia.Parse("### Новое\n- Изменение.\n\n  ![Вид арены]("+url+")\n\n- Другой пункт.","1.0.0");
        Check(blocks.Length==3&&blocks[0].Text=="Новое\n• Изменение."&&blocks[1].Kind=="image"&&blocks[2].Text=="• Другой пункт.","images retain bullet order, caption and readable text");
        Check(ReleaseHistoryMedia.Parse("![x]("+url+")","2.0.0").All(x=>x.Kind!="image"),"cross-release images not fetched");
        Check(ReleaseHistoryMedia.Parse("![x](https://example.com/a.png)","1.0.0").All(x=>x.Kind!="image"),"arbitrary external links remain inert");
        foreach(var invalid in new[]{Png(1601),data[..^1],new byte[ReleaseHistoryMedia.MaxBytes+1]})
        {try{ReleaseHistoryMedia.Validate(invalid,Convert.ToHexString(SHA256.HashData(invalid)));throw new Exception("Invalid image accepted");}catch(InvalidDataException){count++;}}
        string longNotes=new string('Ж',64000)+"\n\n![Кадр]("+url+")";
        var compact=ReleaseHistoryMedia.Parse(longNotes,"1.0.0",compact:true);
        var longItem=new ReleaseHistoryItem("1.0.0","2026-10-30",longNotes,"English",true,RussianBlocks:compact);
        Check(compact.Where(x=>x.Kind=="text").All(x=>x.Text==""&&x.Length>0)&&System.Text.Encoding.UTF8.GetByteCount(System.Text.Json.JsonSerializer.Serialize(Enumerable.Repeat(longItem,11)))<8*1024*1024,"compact note slices keep maximal history within existing 8 MiB IPC bound");
        string root=Path.Combine(Path.GetTempPath(),"history-media-"+Guid.NewGuid().ToString("N"));int requests=0;
        try
        {
            using(var media=new ReleaseHistoryMedia(root,new Transport((request,_)=>{
                requests++;Check(request.RequestUri!.AbsoluteUri==url&&!request.Headers.Contains("Authorization"),"fixed release media URL without credentials");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(data)});
            })))
            {
                var loaded=await media.Load(blocks[1],"1.0.0",default);
                Check(loaded.State=="ready"&&loaded.Width==2&&loaded.Height==1&&File.ReadAllBytes(loaded.Path).SequenceEqual(data),"validated PNG atomically cached with dimensions");
            }
            using(var media=new ReleaseHistoryMedia(root,new Transport((_,_)=>throw new HttpRequestException("offline"))))
            {
                var cached=await media.Load(blocks[1],"1.0.0",default);Check(cached.State=="ready"&&requests==1,"offline revisit uses validated disk bytes");
                File.WriteAllText(cached.Path,"corrupt");
                Check((await media.Load(blocks[1],"1.0.0",default)).State=="unavailable","corrupt cache plus offline falls back without hiding caption");
            }
            using(var media=new ReleaseHistoryMedia(root,new Transport((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect){Headers={Location=new Uri("https://example.com/leak")}}))))
                Check((await media.Load(blocks[1],"1.0.0",default)).State=="unavailable","unapproved redirect rejected");
            using(var media=new ReleaseHistoryMedia(root,new Transport((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Png(3))}))))
                Check((await media.Load(blocks[1],"1.0.0",default)).State=="unavailable","different PNG bytes rejected by expected digest");
            int mediaRequests=0;
            var config=new LauncherConfig("tech.test","osx","arm64","1.0.0","2026-10-30","","game","production");
            string notes="## Русский\n### Новое\n- Изменение.\n\n![Вид]("+url+")\n\n## English\n### Added\n- Change.\n\n![View]("+url+")";
            string metadata=System.Text.Json.JsonSerializer.Serialize(new[]{new{tag_name="v1.0.0",draft=false,prerelease=false,published_at="2026-10-30T00:00:00Z",body=notes}});
            string historyCache=Path.Combine(root,"notes.json");
            using(var history=new ReleaseHistory(config,historyCache,new Transport((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(metadata)})),new Transport((_,_)=>{mediaRequests++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(data)});})))
            {
                var fetched=await history.Fetch(default);Check(mediaRequests==0&&fetched.Items[0].RussianBlocks!.Any(x=>x.Kind=="image"),"metadata fetch parses ordered blocks without prefetching pictures");
                Check(await history.FetchMedia("9.9.9",default)==null&&mediaRequests==0,"unknown selection cannot start media requests");
                var selected=await history.FetchMedia("1.0.0",default);
                Check(mediaRequests==1&&selected!.Items[0].RussianBlocks!.Single(x=>x.Kind=="image").State=="ready"&&selected.Items[0].EnglishBlocks!.Single(x=>x.Kind=="image").Text=="View","selected bilingual media deduplicated and captions retained across IPC");
            }
            using(var history=new ReleaseHistory(config,historyCache,new Transport((_,_)=>throw new HttpRequestException("offline")),new Transport((_,_)=>throw new HttpRequestException("offline"))))
            {
                Check((await history.Fetch(default)).State=="cached","offline metadata cache preserves release identity");
                Check((await history.FetchMedia("1.0.0",default))!.Items[0].RussianBlocks!.Single(x=>x.Kind=="image").State=="ready","new launcher session resolves offline pictures from stable disk cache");
            }
            var began=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var drain=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var mediaWaiting=new ReleaseHistoryMedia(root,new Transport(async(_,token)=>{began.TrySetResult();try{await Task.Delay(Timeout.Infinite,token);}finally{await drain.Task;}return new(HttpStatusCode.OK);}));
            var snapshot=new ReleaseHistorySnapshot("ready",new[]{new ReleaseHistoryItem("1.0.0","2026-10-30","RU","EN",true)});
            await using var session=new UpdateSession(_=>Task.FromResult<string?>(null),(_,_)=>Task.CompletedTask,()=>{},_=>{},historyFetch:_=>Task.FromResult(snapshot),mediaFetch:async(version,token)=>{await mediaWaiting.Load(blocks[1],version,token);return snapshot;});
            session.Start();await session.CommandAsync("history");await session.CommandAsync("history-media","1.0.0");await began.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var match=session.CommandAsync("match","media-drain");Check(!match.IsCompleted&&!session.State.MatchPaused,"match waits for media HTTP cancellation drain");drain.TrySetResult();await match.WaitAsync(TimeSpan.FromSeconds(5));Check(session.State.MatchPaused,"match admitted after media drained");
            await session.CommandAsync("history-close");await session.CommandAsync("menu","media-drain");
            foreach(bool close in new[]{true,false})
            {
                int calls=0;var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var released=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var two=snapshot with{Items=snapshot.Items.Concat(new[]{snapshot.Items[0] with{Version="2.0.0"}}).ToArray()};
                await using var selections=new UpdateSession(_=>Task.FromResult<string?>(null),(_,_)=>Task.CompletedTask,()=>{},_=>{},historyFetch:_=>Task.FromResult(two),mediaFetch:async(version,token)=>{
                    calls++;if(calls==1){entered.TrySetResult();try{await Task.Delay(Timeout.Infinite,token);}catch(OperationCanceledException){}await released.Task;return two with{Error="late"};}
                    return two with{Error="selected"};
                });
                selections.Start();await selections.CommandAsync("history");await selections.CommandAsync("history-media","1.0.0");await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var change=selections.CommandAsync(close?"history-close":"history-media","2.0.0");
                Check(!change.IsCompleted,"closing or changing release waits for previous media drain");released.TrySetResult();await change.WaitAsync(TimeSpan.FromSeconds(5));
                Check(selections.State.History!.Error!="late","late cancelled media cannot replace selected history");
                if(close){await selections.CommandAsync("history-media","1.0.0");await selections.CommandAsync("menu");Check(calls==1,"closed history neither fetches nor resumes pictures in menu");}
                else Check(calls==2&&selections.State.History.Error=="selected","only new release selection publishes after old drain");
            }
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
        Console.WriteLine(count+" focused history-media checks passed");
    }
    sealed class Transport(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> action):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>action(request,token);}
}

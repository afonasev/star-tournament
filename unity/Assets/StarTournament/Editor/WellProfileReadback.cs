using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

// Offline only; compile this file in a dedicated minimal Editor project.
public static class WellProfileReadback
{
    sealed class Stat{public long calls;public double inclusive,self;}
    static string Q(string s)=>"\""+(s??"").Replace("\"","\"\"")+"\"";
    static string N(double n)=>n.ToString("R",CultureInfo.InvariantCulture);
    public static void Read()
    {
        try
        {
            string input=Environment.GetEnvironmentVariable("WELL_PROFILE_INPUT"),output=Environment.GetEnvironmentVariable("WELL_PROFILE_OUTPUT");
            if(!Path.IsPathFullyQualified(input)||!File.Exists(input)||!Path.IsPathFullyQualified(output)||Directory.Exists(output))throw new InvalidOperationException("New absolute output and existing raw required");
            Directory.CreateDirectory(output);
            if(!ProfilerDriver.LoadProfile(input,false))throw new IOException("ProfilerDriver rejected raw");
            int first=ProfilerDriver.firstFrameIndex,last=ProfilerDriver.lastFrameIndex;
            if(first<0||last<first)throw new IOException("No readable CPU frames");
            var summary=new Dictionary<string,Stat>();long frameThreads=0,totalSamples=0,negativeSelf=0,unnamedSamples=0;
            using(var frames=new StreamWriter(Path.Combine(output,"frames.csv")))
            using(var markers=new StreamWriter(Path.Combine(output,"markers.csv")))
            {
                frames.WriteLine("frame,thread_index,thread,frame_start_ms,cpu_frame_ms,gpu_frame_ms,samples");
                markers.WriteLine("frame,thread,marker,calls,inclusive_ms,self_ms");
                for(int frame=first;frame<=last;frame++)
                for(int thread=0;;thread++)
                using(var view=ProfilerDriver.GetRawFrameDataView(frame,thread))
                {
                    if(!view.valid)break;
                    frameThreads++;totalSamples+=view.sampleCount;
                    frames.WriteLine(frame+","+thread+","+Q(view.threadName)+","+N(view.frameStartTimeMs)+","+N(view.frameTimeMs)+","+N(view.frameGpuTimeMs)+","+view.sampleCount);
                    var names=new Dictionary<int,string>();var stats=new Dictionary<string,Stat>();
                    for(int i=0;i<view.sampleCount;i++)
                    {
                        int id=view.GetSampleMarkerId(i);
                        if(!names.TryGetValue(id,out var name)){name=view.GetSampleName(i)??("<unnamed marker "+id+">");names[id]=name;}
                        if(name.StartsWith("<unnamed marker "))unnamedSamples++;
                        double duration=view.GetSampleTimeMs(i),self=duration;
                        int child=i+1;
                        for(int c=0;c<view.GetSampleChildrenCount(i);c++)
                        {
                            if(child>=view.sampleCount)throw new IOException("Invalid child range");
                            self-=view.GetSampleTimeMs(child);child+=1+view.GetSampleChildrenCountRecursive(child);
                        }
                        if(self<-.001)negativeSelf++;
                        if(!stats.TryGetValue(name,out var stat)){stat=new Stat();stats[name]=stat;}
                        stat.calls++;stat.inclusive+=duration;stat.self+=Math.Max(0,self);
                    }
                    foreach(var pair in stats)
                    {
                        markers.WriteLine(frame+","+Q(view.threadName)+","+Q(pair.Key)+","+pair.Value.calls+","+N(pair.Value.inclusive)+","+N(pair.Value.self));
                        string key=view.threadName+"/"+pair.Key;
                        if(!summary.TryGetValue(key,out var all)){all=new Stat();summary[key]=all;}
                        all.calls+=pair.Value.calls;all.inclusive+=pair.Value.inclusive;all.self+=pair.Value.self;
                    }
                }
            }
            if(frameThreads==0||totalSamples==0)throw new IOException("Empty parsed profile");
            using(var all=new StreamWriter(Path.Combine(output,"summary.csv")))
            {
                all.WriteLine("thread_marker,calls,inclusive_ms,self_ms");
                foreach(var p in summary.OrderByDescending(p=>p.Value.self))all.WriteLine(Q(p.Key)+","+p.Value.calls+","+N(p.Value.inclusive)+","+N(p.Value.self));
            }
            File.WriteAllText(Path.Combine(output,"status.json"),"{\"status\":\"READBACK_COMPLETE_NOT_PERFORMANCE_ACCEPTANCE\",\"first\":"+first+",\"last\":"+last+",\"frameThreads\":"+frameThreads+",\"totalSamples\":"+totalSamples+",\"unnamedSamples\":"+unnamedSamples+",\"negativeSelfClamped\":"+negativeSelf+"}");
            Debug.Log("WELL_PROFILE_READBACK_PASS frames="+(last-first+1));EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}

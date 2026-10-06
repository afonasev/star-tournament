#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    // Review-only listener filter: retain the native mix, then silence the output.
    // The DSP callback does no file I/O, allocation or Unity API calls.
    public sealed class NativeMusicCapture : MonoBehaviour
    {
        readonly object sync=new object();
        float[] buffer;int count,channels,rate;bool recording,overflow;
        public void Begin()
        {
            rate=AudioSettings.outputSampleRate;
            buffer=new float[rate*8*60];recording=true;
        }
        void OnAudioFilterRead(float[] data,int channelCount)
        {
            lock(sync)
            {
                if(recording&&buffer!=null)
                {
                    if(channels!=0&&channels!=channelCount)overflow=true;
                    channels=channelCount;
                    int remaining=buffer.Length-count;
                    int take=Math.Min(remaining,data.Length);
                    Array.Copy(data,0,buffer,count,take);count+=take;
                    if(take!=data.Length)overflow=true;
                }
            }
            Array.Clear(data,0,data.Length);
        }
        public string Save(string path)
        {
            lock(sync){recording=false;}
            if(overflow||channels==0||count<rate*channels)throw new InvalidOperationException("Native audio capture missing, overflowing or changed channel format");
            double peak=0;int clipped=0;
            for(int i=0;i<count;i++)
            {
                if(float.IsNaN(buffer[i])||float.IsInfinity(buffer[i]))throw new InvalidOperationException("Non-finite native audio sample");
                peak=Math.Max(peak,Math.Abs(buffer[i]));if(Math.Abs(buffer[i])>=1)clipped++;
            }
            if(peak<.00001||clipped!=0)throw new InvalidOperationException("Native audio capture silent or clipped: peak="+peak+" clipped="+clipped);
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*4);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((ushort)3);
                writer.Write((ushort)channels);writer.Write(rate);writer.Write(rate*channels*4);writer.Write((ushort)(channels*4));writer.Write((ushort)32);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*4);
                for(int i=0;i<count;i++)writer.Write(buffer[i]);
            }
            return "native listener WAV: channels="+channels+" rate="+rate+" seconds="+(count/(double)(rate*channels)).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" peak="+peak.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" clipped="+clipped;
        }
    }
}
#endif

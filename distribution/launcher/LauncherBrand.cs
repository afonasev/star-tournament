namespace StarTournament.Distribution;

// The approved existing game icon is embedded, so bootstrap/maintenance never
// depend on a sidecar path or network to paint the brand.
public static class LauncherBrand
{
    static readonly byte[] icon = Read();
    public static string DataUri { get; } = "data:image/x-icon;base64,"+Convert.ToBase64String(icon);
    static byte[] Read()
    {
        using var stream=typeof(LauncherBrand).Assembly.GetManifestResourceStream("StarTournament.LauncherLogo.ico")
            ?? throw new IOException("Embedded launcher logo missing");
        using var copy=new MemoryStream();stream.CopyTo(copy);return copy.ToArray();
    }
    public static byte[] IconPixels()
    {
        using var reader=new BinaryReader(new MemoryStream(icon,false));
        if(reader.ReadUInt16()!=0||reader.ReadUInt16()!=1)throw new IOException("Invalid embedded icon");
        int count=reader.ReadUInt16(),bestArea=0;uint size=0,offset=0;
        for(int i=0;i<count;i++)
        {
            int width=reader.ReadByte(),height=reader.ReadByte();
            if(width==0)width=256;if(height==0)height=256;
            reader.ReadBytes(6);uint length=reader.ReadUInt32(),position=reader.ReadUInt32();
            if(width*height>bestArea){bestArea=width*height;size=length;offset=position;}
        }
        if(size==0||(long)offset+size>icon.Length)throw new IOException("Invalid embedded icon bounds");
        return icon.AsSpan(checked((int)offset),checked((int)size)).ToArray();
    }
}

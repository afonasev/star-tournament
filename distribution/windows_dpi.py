"""Inspect the embedded Windows process manifest (ported from star-racing 24231df4)."""
from pathlib import Path
import struct
import xml.etree.ElementTree as ET


def windows_manifest(executable):
    """Read the process manifest from PE resources, not an incidental XML string."""
    data=Path(executable).read_bytes()
    try:
        if data[:2]!=b'MZ':raise ValueError('Not a Windows PE executable')
        pe=struct.unpack_from('<I',data,0x3c)[0]
        if data[pe:pe+4]!=b'PE\0\0':raise ValueError('Invalid PE header')
        count=struct.unpack_from('<H',data,pe+6)[0]
        optional_size=struct.unpack_from('<H',data,pe+20)[0];optional=pe+24
        magic=struct.unpack_from('<H',data,optional)[0]
        if magic not in (0x10b,0x20b):raise ValueError('Unsupported PE optional header')
        directory=optional+(112 if magic==0x20b else 96)
        resource_rva=struct.unpack_from('<I',data,directory+16)[0]
        sections=optional+optional_size
        def offset(rva):
            for i in range(count):
                _,address,size,start=struct.unpack_from('<IIII',data,sections+i*40+8)
                if address<=rva<address+size:return start+rva-address
            raise ValueError('PE resource RVA is outside file-backed sections')
        root=offset(resource_rva)
        def entries(at):
            named,ids=struct.unpack_from('<HH',data,at+12)
            return [struct.unpack_from('<II',data,at+16+i*8) for i in range(named+ids)]
        def child(at,key):
            for ident,value in entries(at):
                if ident==key and value&0x80000000:return root+(value&0x7fffffff)
            raise ValueError('Windows launcher process manifest is missing')
        languages=entries(child(child(root,24),1))
        if not languages or languages[0][1]&0x80000000:raise ValueError('Invalid manifest language resource')
        rva,size=struct.unpack_from('<II',data,root+languages[0][1])
        start=offset(rva)
        if start+size>len(data):raise ValueError('Truncated manifest resource')
        return data[start:start+size]
    except struct.error as error:
        raise ValueError('Truncated Windows PE resource table') from error


def validate_windows_dpi(executable):
    manifest=ET.fromstring(windows_manifest(executable))
    legacy=manifest.find('.//{http://schemas.microsoft.com/SMI/2005/WindowsSettings}dpiAware')
    modern=manifest.find('.//{http://schemas.microsoft.com/SMI/2016/WindowsSettings}dpiAwareness')
    if legacy is None or legacy.text!='true' or modern is None or modern.text!='PerMonitorV2, PerMonitor':
        raise ValueError('Windows launcher must declare per-monitor DPI awareness')



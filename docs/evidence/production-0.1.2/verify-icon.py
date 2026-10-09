"""Verify every embedded PE icon group against the unchanged approved ICO bytes."""
import hashlib,json,struct,sys
from pathlib import Path
exe,ico=map(Path,sys.argv[1:3]);b=exe.read_bytes();i=ico.read_bytes()
u16=lambda data,pos:struct.unpack_from('<H',data,pos)[0]
u32=lambda data,pos:struct.unpack_from('<I',data,pos)[0]
pe=u32(b,0x3c);assert b[pe:pe+4]==b'PE\0\0';optional=pe+24
section_count=u16(b,pe+6);section_start=optional+u16(b,pe+20)
sections=[struct.unpack_from('<IIII',b,section_start+n*40+8) for n in range(section_count)]
def offset(rva):
 for virtual_size,virtual_address,raw_size,raw_pointer in sections:
  if virtual_address<=rva<virtual_address+max(virtual_size,raw_size):return raw_pointer+rva-virtual_address
 raise ValueError('RVA outside sections')
data_directory=optional+(96 if u16(b,optional)==0x10b else 112)
base=offset(u32(b,data_directory+16));resources={}
def walk(position,path):
 count=u16(b,base+position+12)+u16(b,base+position+14)
 for n in range(count):
  name,entry=struct.unpack_from('<II',b,base+position+16+n*8)
  assert not name&0x80000000,'Unexpected named resource'
  key=path+(name,)
  if entry&0x80000000:walk(entry&0x7fffffff,key)
  else:
   rva,size=struct.unpack_from('<II',b,base+entry);start=offset(rva);resources[key]=b[start:start+size]
walk(0,())
assert i[:4]==b'\0\0\1\0';count=u16(i,4);expected=[]
for n in range(count):
 p=6+n*16;size,start=struct.unpack_from('<II',i,p+8);expected.append((i[p:p+12],i[start:start+size]))
groups=[]
for key,group in resources.items():
 if key[0]!=14:continue
 assert group[:6]==i[:6],(key,'icon group header/count mismatch')
 icons=[]
 for n,(metadata,content) in enumerate(expected):
  p=6+n*14;assert group[p:p+12]==metadata,(key,n,'metadata mismatch')
  icon_id=u16(group,p+12);actual=resources[(3,icon_id,key[2])];assert actual==content,(key,n,'image bytes mismatch')
  icons.append({'width':metadata[0] or 256,'height':metadata[1] or 256,'sha256':hashlib.sha256(actual).hexdigest()})
 groups.append({'resource':key,'icons':icons})
assert groups,'No embedded PE icon groups'
print(json.dumps({'result':'Passed','installer_sha256':hashlib.sha256(b).hexdigest(),'approved_ico_sha256':hashlib.sha256(i).hexdigest(),'groups':groups,'physical_Explorer_wizard_acceptance':False},indent=2))

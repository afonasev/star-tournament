from pathlib import Path
import plistlib, subprocess
p=Path("unity/Builds/StarTournamentProvingGround.app")
f=p/"Contents/Info.plist"
d=plistlib.loads(f.read_bytes());d["CFBundleIdentifier"]="com.afonasev.star-tournament-menu-f2-qa";d["CFBundleName"]="Star Tournament F2 QA";d["CFBundleDisplayName"]="Star Tournament F2 QA"
f.write_bytes(plistlib.dumps(d))
subprocess.run(["codesign","--force","--deep","--sign","-",str(p)],check=True)

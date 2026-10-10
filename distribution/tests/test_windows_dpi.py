import shutil
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET

sys.path.insert(0,str(Path(__file__).parents[1]))
from windows_dpi import windows_manifest,validate_windows_dpi

ROOT=Path(__file__).parents[1]

class WindowsDpiContract(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.compiler=shutil.which('x86_64-w64-mingw32-g++')
        cls.windres=shutil.which('x86_64-w64-mingw32-windres')
        if not cls.compiler or not cls.windres:
            raise RuntimeError('Native resource QA requires mingw compiler and windres')
        cls.temp=tempfile.TemporaryDirectory(prefix='star-tournament-dpi-')
        cls.addClassCleanup(cls.temp.cleanup)
        cls.directory=Path(cls.temp.name)/'resources with spaces';cls.directory.mkdir()
        cls.source=cls.directory/'probe.cpp';cls.source.write_text('int main(){return 0;}\n')

    def compile_probe(self,name,manifest=None):
        resource=self.directory/(name+'.o');rc=self.directory/(name+'.rc')
        icon=ROOT/'branding/app-icon.ico'
        script=('1 ICON "'+icon.as_posix()+'"\n1 24 "'+Path(manifest).resolve().as_posix()+'"\n') if manifest else '1 ICON "'+icon.as_posix()+'"\n'
        rc.write_text(script)
        subprocess.run([self.windres,str(rc),str(resource)],check=True,capture_output=True)
        exe=self.directory/(name+'.exe')
        subprocess.run([self.compiler,str(self.source),str(resource),'-o',str(exe)],check=True,capture_output=True)
        return exe

    def test_compiled_resource_declares_per_monitor_dpi(self):
        manifest=self.directory/'application with spaces.manifest'
        manifest.write_bytes((ROOT/'launcher/app.manifest').read_bytes())
        exe=self.compile_probe('aware',manifest)
        validate_windows_dpi(exe)
        self.assertEqual(ET.tostring(ET.fromstring(windows_manifest(exe))),ET.tostring(ET.fromstring(manifest.read_bytes())))

    def test_actual_dotnet_launcher_embeds_manifest(self):
        dotnet=os.environ.get('DOTNET') or shutil.which('dotnet')
        if not dotnet:
            self.fail('Launcher resource QA requires DOTNET or dotnet on PATH')
        output=self.directory/'launcher'
        subprocess.run([dotnet,'publish',str(ROOT/'launcher/Launcher.csproj'),
                        '-c','Release','-r','win-x64','--self-contained','true',
                        '-p:DebugType=None','-p:OutputType=WinExe',
                        '--artifacts-path',str(self.directory/'dotnet-artifacts'),
                        '-o',str(output)],check=True,capture_output=True)
        exe=output/'StarTournamentLauncher.exe'
        validate_windows_dpi(exe)
        manifest=ET.fromstring(windows_manifest(exe))
        privilege=manifest.find('.//{urn:schemas-microsoft-com:asm.v3}requestedExecutionLevel')
        self.assertIsNotNone(privilege)
        self.assertEqual(privilege.attrib,{'level':'asInvoker','uiAccess':'false'})

    def test_previous_icon_only_bootstrap_is_rejected(self):
        exe=self.compile_probe('previous')
        with self.assertRaisesRegex(ValueError,'manifest is missing'):
            validate_windows_dpi(exe)

    def test_incidental_xml_is_not_a_process_manifest(self):
        exe=self.compile_probe('appended')
        with exe.open('ab') as stream:stream.write((ROOT/'launcher/app.manifest').read_bytes())
        with self.assertRaisesRegex(ValueError,'manifest is missing'):
            validate_windows_dpi(exe)

    def test_unaware_embedded_manifest_is_rejected(self):
        manifest=self.directory/'unaware.manifest'
        manifest.write_text((ROOT/'launcher/app.manifest').read_text().replace('PerMonitorV2, PerMonitor','unaware'))
        exe=self.compile_probe('unaware',manifest)
        with self.assertRaisesRegex(ValueError,'per-monitor DPI awareness'):
            validate_windows_dpi(exe)

    def test_truncated_executable_is_rejected(self):
        exe=self.directory/'truncated.exe';exe.write_bytes(b'MZ')
        with self.assertRaisesRegex(ValueError,'Truncated Windows PE'):
            validate_windows_dpi(exe)

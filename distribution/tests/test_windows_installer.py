"""Focused checks for the Windows NSIS packaging contract."""
import importlib.util
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch


ROOT = Path(__file__).parents[1]
SPEC = importlib.util.spec_from_file_location("windows_installer", ROOT / "windows" / "build_installer.py")
installer = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(installer)


class WindowsInstallerBuildTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.layout = self.root / "portable input"
        for relative in installer.REQUIRED_FILES:
            path = self.layout / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"fixture")
        (self.layout / ".portable").write_bytes(b"")

    def test_accepts_extracted_portable_layout_and_preserves_dot_marker(self):
        self.assertEqual(installer.validate_layout(self.layout), self.layout.resolve())

    def test_rejects_missing_portable_marker(self):
        (self.layout / ".portable").unlink()
        with self.assertRaisesRegex(installer.BuildInputError, r"\.portable"):
            installer.validate_layout(self.layout)

    def test_rejects_incomplete_layout(self):
        (self.layout / "maintenance" / "maintenance.json").unlink()
        with self.assertRaisesRegex(installer.BuildInputError, "maintenance/maintenance.json"):
            installer.validate_layout(self.layout)

    def test_rejects_symlinked_layout_content(self):
        link = self.layout / "maintenance" / "linked-runtime.dll"
        link.symlink_to(self.layout / "maintenance" / "maintenance.json")
        with self.assertRaisesRegex(installer.BuildInputError, "symbolic links"):
            installer.validate_layout(self.layout)

    def test_output_is_named_windows_setup_exe(self):
        result = installer.normalize_output(self.root / "release output")
        self.assertEqual(result.name, "Star-Tournament-1.2.3-Windows-x64-Setup.exe")
        self.assertTrue(result.parent.is_dir())
        with self.assertRaisesRegex(installer.BuildInputError, "filename must include Star Tournament"):
            installer.normalize_output(self.root / "wrong-name.exe")

    def test_compiler_receives_exact_defines_and_script(self):
        output = self.root / "out" / "WindowsSetup.exe"
        command = installer.compiler_command("makensis", ROOT / "windows" / "installer.nsi",
                                             self.layout.resolve(), "1.2.3-rc.4", output)
        define = "/D" if installer.os.name == "nt" else "-D"
        self.assertEqual(command[0], "makensis")
        self.assertIn(f"{define}INPUT_DIR={self.layout.resolve()}", command)
        self.assertIn(f"{define}VERSION=1.2.3-rc.4", command)
        self.assertIn(f"{define}OUTPUT_FILE={output}", command)
        self.assertEqual(command[-1], str((ROOT / "windows" / "installer.nsi").resolve()))

    def test_rejects_non_semver_before_compilation(self):
        with self.assertRaisesRegex(installer.BuildInputError, "Invalid semantic version"):
            installer.compiler_command("makensis", ROOT / "windows" / "installer.nsi",
                                       self.layout, "01.2.3", self.root / "WindowsSetup.exe")

    def test_main_calls_compiler_and_requires_output(self):
        output_dir = self.root / "release"
        created = output_dir / "Star-Tournament-1.2.3-Windows-x64-Setup.exe"

        def compile_installer(command, check):
            self.assertTrue(check)
            created.write_bytes(b"setup")

        with patch.object(installer.subprocess, "run", side_effect=compile_installer) as run:
            result = installer.main(["--layout", str(self.layout), "--version", "1.2.3",
                                     "--output", str(output_dir), "--makensis", "makensis"])
        self.assertEqual(result, 0)
        self.assertEqual(run.call_count, 1)
        self.assertTrue(created.is_file())

    def test_remote_compiler_host_is_fixed_to_approved_alias(self):
        def fake_remote(layout, script, version, output):
            output.write_bytes(b"setup")

        with patch.object(installer, "remote_compile", side_effect=fake_remote) as compile_remote:
            result = installer.main(["--layout", str(self.layout), "--version", "1.2.3",
                                     "--output", str(self.root / "remote"),
                                     "--compiler-host", "gfe"])
        self.assertEqual(result, 0)
        compile_remote.assert_called_once()
        with self.assertRaises(SystemExit):
            installer.main(["--layout", str(self.layout), "--version", "1.2.3",
                            "--output", str(self.root / "remote"),
                            "--compiler-host", "other-host"])


class WindowsInstallerScriptContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = (ROOT / "windows" / "installer.nsi").read_text()

    def test_admin_mui2_wizard_and_default_finish_launch(self):
        self.assertIn("RequestExecutionLevel admin", self.source)
        self.assertIn('!include "MUI2.nsh"', self.source)
        for page in ("MUI_PAGE_DIRECTORY", "MUI_PAGE_FINISH"):
            self.assertIn(f"!insertmacro {page}", self.source)
        self.assertIn("!define MUI_FINISHPAGE_RUN\n", self.source)
        self.assertIn("MUI_FINISHPAGE_RUN_FUNCTION LaunchInstalledGame", self.source)
        self.assertNotIn("MUI_FINISHPAGE_RUN_NOTCHECKED", self.source)
        self.assertNotIn("MUI_PAGE_COMPONENTS", self.source)
        self.assertIn("MUI_FINISHPAGE_SHOWREADME_FUNCTION CreateDesktopShortcut", self.source)
        self.assertNotIn("MUI_FINISHPAGE_SHOWREADME_NOTCHECKED", self.source)
        self.assertRegex(self.source, r'Section "Star Tournament \(обязательный компонент\)" MainInstall\n\s+SectionIn RO')
        self.assertIn("IfErrors desktop_shortcut_failed", self.source)
        self.assertIn('IfFileExists "$DESKTOP\\Star Tournament.lnk" desktop_shortcut_done desktop_shortcut_failed', self.source)
        self.assertIn('InstallDir "$PROGRAMFILES64\\Star Tournament"', self.source)
        self.assertIn('SetShellVarContext all', self.source)
        self.assertIn('!insertmacro MUI_LANGUAGE "Russian"', self.source)

    def test_broker_guards_and_stable_launch_path_are_used(self):
        for operation in ("--validate-root", "--check-idle", "--check-install"):
            self.assertIn(operation, self.source)
        self.assertIn('--launch-user "$INSTDIR\\Star Tournament.exe"', self.source)
        self.assertIn("installing.flag", self.source)
        self.assertIn('--check-idle "$INSTDIR"', self.source)
        self.assertIn("--check-legacy-user", self.source)
        self.assertNotIn("ReadRegStr $0 HKCU", self.source)
        self.assertIn('File /r "${INPUT_DIR}\\maintenance\\*"', self.source)
        self.assertIn('ExecWait \'"$PLUGINSDIR\\StarTournamentMaintenance.exe" --check-idle "$INSTDIR"\'', self.source)
        self.assertIn("Star Tournament уже установлена. Обновляйте игру из самой игры", self.source)

    def test_uninstaller_is_marker_gated_and_preserves_unknown_root_files(self):
        self.assertIn('.star-tournament-install.json', self.source)
        self.assertIn('--validate-root "$INSTDIR"', self.source)
        self.assertIn('Delete "$INSTDIR\\.portable"', self.source)
        self.assertIn('RMDir /r "$INSTDIR\\current"', self.source)
        self.assertIn('RMDir /r "$INSTDIR\\packages"', self.source)
        self.assertNotIn('RMDir /r "$INSTDIR"', self.source)
        self.assertNotIn('RMDir /r "$PROFILE', self.source)
        self.assertIn('RMDir "$INSTDIR"', self.source)

    def test_uninstaller_preserves_original_root_and_partial_recovery(self):
        function = self.source.split("Function un.onInit", 1)[1].split("FunctionEnd", 1)[0]
        self.assertNotIn('StrCpy $INSTDIR "$EXEDIR"', function)
        self.assertNotRegex(function, r'(?m)^\s*(?:StrCpy|ReadRegStr|GetFullPathName) \$INSTDIR\b')
        self.assertIn('ReadRegStr $0 HKLM "${PRODUCT_KEY}" "InstallLocation"', function)
        self.assertIn('StrCmp $0 "" un_registry_path_ok', function)
        self.assertIn('StrCmp $0 "$INSTDIR" un_registry_path_ok', function)
        self.assertIn('--validate-root "$INSTDIR"', function)

    def test_filesystem_error_checks_ignore_prior_directory_and_registry_errors(self):
        # FindFirst/FindNext set the persistent NSIS flag at normal enumeration end.
        # ReadRegStr does the same for a partial install without registration.
        for operation, failure in [
            ('CreateDirectory "$INSTDIR\\maintenance"', 'install_lock_failed'),
            ('FileOpen $InstallFlag "$INSTDIR\\${MARKER_NAME}" w', 'install_recovery_setup_failed'),
            ('WriteUninstaller "$INSTDIR\\Uninstall.exe"', 'install_recovery_setup_failed'),
            ('FileOpen $InstallFlag "$INSTDIR\\maintenance\\installing.flag" w', 'un_lock_failed'),
        ]:
            pattern = r'ClearErrors\s+' + re.escape(operation) + r'\s+IfErrors ' + failure
            self.assertRegex(self.source, pattern)

    def test_install_marker_write_is_valid_owned_json(self):
        line = next(line.strip() for line in self.source.splitlines()
                    if line.strip().startswith("FileWrite $InstallFlag ")
                    and "installerVersion" in line)
        literal = line.removeprefix('FileWrite $InstallFlag "').removesuffix('"')
        literal = literal.replace('$\\"', '"').replace('${VERSION}', '1.2.3')
        literal = literal.replace('$\\r$\\n', '')
        self.assertEqual(json.loads(literal), {
            "protocol": 1,
            "product": "Star Tournament",
            "installerVersion": "1.2.3",
        })

    @unittest.skipUnless(shutil.which("makensis"), "makensis is not installed")
    def test_modern_ui_expands_branded_icons_and_checked_finish_actions(self):
        # POSIX makensis cannot emit an installer here; its preprocessor still validates UI macros.
        with tempfile.TemporaryDirectory(prefix="nsis-ppo-") as tmp:
            temp = Path(tmp)
            layout = temp / "layout"
            for relative in installer.REQUIRED_FILES:
                target = layout / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(b"fixture")
            (layout / ".portable").write_bytes(b"")
            script = temp / "installer.nsi"
            source = re.sub(r"[^\x00-\x7f]", "x", self.source)
            source = source.replace('MUI_LANGUAGE "Russian"', 'MUI_LANGUAGE "English"')
            script.write_text(source)
            result = subprocess.run([
                shutil.which("makensis"), "-PPO", f"-DINPUT_DIR={layout}",
                "-DVERSION=1.2.3", f"-DOUTPUT_FILE={temp / 'WindowsSetup.exe'}", str(script),
            ], check=False, text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        # Check the effective commands after MUI_INTERFACE expansion: direct Icon
        # commands before the page macros are overwritten by Modern UI defaults.
        for command in ("Icon", "UninstallIcon"):
            paths = re.findall(r'^' + command + r' "([^"\n]+)"', result.stdout, re.MULTILINE)
            self.assertTrue(paths, f"Missing expanded {command} command")
            self.assertTrue(all(path.replace("\\", "/") == f"{layout}/app-icon.ico"
                                for path in paths), paths)
        self.assertIn("Var mui.FinishPage.Run", result.stdout)
        self.assertIn("SendMessage $mui.FinishPage.Run 0x00F1 1 0", result.stdout)
        self.assertIn('Call "LaunchInstalledGame"', result.stdout)
        self.assertIn('Call "CreateDesktopShortcut"', result.stdout)
        self.assertIn("SendMessage $mui.FinishPage.ShowReadme 0x00F1 1 0", result.stdout)


if __name__ == "__main__":
    unittest.main()

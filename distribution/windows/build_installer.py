#!/usr/bin/env python3
"""Build the all-users NSIS installer from an extracted Velopack Portable layout."""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import re
import shlex
import subprocess
import sys
import tempfile
import uuid
import zipfile
import hashlib


VERSION_RE = re.compile(
    r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
    r"(?:-((?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*))?"
    r"(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$"
)


REQUIRED_FILES = (
    Path("Star Tournament.exe"),
    Path("Update.exe"),
    Path("app-icon.ico"),
    Path("current/release.json"),
    Path("current/sq.version"),
    Path("current/StarTournamentLauncher.exe"),
    Path("maintenance/StarTournamentMaintenance.exe"),
    Path("maintenance/maintenance.json"),
    Path("maintenance/update-public.pem"),
)


class BuildInputError(ValueError):
    """A user-provided layout, version, or output path is invalid."""


def validate_layout(layout: Path) -> Path:
    layout = layout.expanduser().resolve()
    if not layout.is_dir():
        raise BuildInputError(f"Layout directory does not exist: {layout}")
    if any(path.is_symlink() for path in (layout, *layout.rglob("*"))):
        raise BuildInputError("Layout must not contain symbolic links")
    missing = [str(relative) for relative in REQUIRED_FILES if not (layout / relative).is_file()]
    if missing:
        raise BuildInputError("Layout is missing required files: " + ", ".join(missing))
    if not (layout / ".portable").is_file():
        raise BuildInputError("Layout is missing the Velopack .portable marker file")
    return layout


def normalize_output(output: Path, version: str = "1.2.3") -> Path:
    output = output.expanduser()
    name = f"Star-Tournament-{version}-Windows-x64-Setup.exe"
    if output.suffix.lower() == ".exe":
        if output.name != name:
            raise BuildInputError("Installer filename must include Star Tournament, version and platform")
        output_file = output.resolve()
    else:
        output_file = (output / name).resolve()
    output_file.parent.mkdir(parents=True, exist_ok=True)
    return output_file


def compiler_command(makensis: str, script: Path, layout: Path, version: str,
                     output_file: Path) -> list[str]:
    if not VERSION_RE.fullmatch(version):
        raise BuildInputError(f"Invalid semantic version: {version}")
    define = "/D" if os.name == "nt" else "-D"
    return [
        makensis,
        f"{define}INPUT_DIR={layout}",
        f"{define}VERSION={version}",
        f"{define}OUTPUT_FILE={output_file}",
    ] + (["/INPUTCHARSET", "UTF8"] if os.name == "nt" else ["-INPUTCHARSET", "UTF8"]) + [
        str(script),
    ]


def remote_compile(layout: Path, script: Path, version: str, output_file: Path) -> None:
    """Compile on the approved gfe host, staging only the public installer inputs."""
    remote_root = f"/var/tmp/star-tournament-installer-{uuid.uuid4().hex}"
    remote_archive = f"{remote_root}/inputs.zip"
    remote_script = f"{remote_root}/compile.py"
    remote_output = f"{remote_root}/WindowsSetup.exe"
    output_file.parent.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="star-tournament-installer-") as local_tmp:
        local_archive = Path(local_tmp) / "inputs.zip"
        with zipfile.ZipFile(local_archive, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for path in sorted(layout.rglob("*")):
                if path.is_file():
                    archive.write(path, Path("layout") / path.relative_to(layout))
            archive.write(script, "installer.nsi")

        extractor = (
            "import pathlib,sys,zipfile;"
            "root=pathlib.Path(sys.argv[1]).resolve();"
            "z=zipfile.ZipFile(sys.argv[2]);"
            "[(lambda p: (_ for _ in ()).throw(SystemExit('unsafe archive path')) "
            "if p.is_absolute() or '..' in p.parts else None)(pathlib.PurePosixPath(i.filename)) "
            "for i in z.infolist()];"
            "[(lambda d: (_ for _ in ()).throw(SystemExit('unsafe archive destination')) "
            "if d != root and root not in d.parents else None)("
            "(root/pathlib.Path(*pathlib.PurePosixPath(i.filename).parts)).resolve()) "
            "for i in z.infolist()];z.extractall(root)"
        )
        compiler = """import pathlib,subprocess,sys
root=pathlib.Path(sys.argv[1]); version=sys.argv[2]; output=sys.argv[3]
command=['nice','-n','19','makensis','-INPUTCHARSET','UTF8',
 '-DINPUT_DIR='+str(root/'layout'),'-DVERSION='+version,'-DOUTPUT_FILE='+output,
 str(root/'installer.nsi')]
print('Remote compiler: makensis 3.10 (Ubuntu package)',flush=True)
raise SystemExit(subprocess.run(command,cwd=root).returncode)
"""
        remote_script_local = Path(local_tmp) / "compile.py"
        remote_script_local.write_text(compiler)
        try:
            subprocess.run(["ssh", "gfe", "mkdir", "-m", "700", remote_root], check=True)
            subprocess.run(["scp", str(local_archive), f"gfe:{remote_archive}"], check=True)
            subprocess.run(["scp", str(remote_script_local), f"gfe:{remote_script}"], check=True)
            extract_command = f"python3 -c {shlex.quote(extractor)} {shlex.quote(remote_root)} {shlex.quote(remote_archive)}"
            compile_command = f"python3 {shlex.quote(remote_script)} {shlex.quote(remote_root)} {shlex.quote(version)} {shlex.quote(remote_output)}"
            subprocess.run(["ssh", "gfe", f"{extract_command} && {compile_command}"], check=True)
            subprocess.run(["scp", f"gfe:{remote_output}", str(output_file)], check=True)
            remote_hash = subprocess.run(
                ["ssh", "gfe", "sha256sum", remote_output], check=True,
                text=True, capture_output=True,
            ).stdout.split()[0]
            local_hash = hashlib.sha256(output_file.read_bytes()).hexdigest()
            if local_hash != remote_hash:
                output_file.unlink(missing_ok=True)
                raise BuildInputError("Remote installer SHA-256 verification failed")
        finally:
            subprocess.run(["ssh", "gfe", "rm", "-rf", "--", remote_root],
                           check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--layout", type=Path, required=True,
                        help="extracted Velopack Portable ZIP directory")
    parser.add_argument("--version", required=True, help="installer semantic version")
    parser.add_argument("--output", type=Path, required=True,
                        help="WindowsSetup.exe path or output directory")
    parser.add_argument("--makensis", help="path or command name for local makensis")
    parser.add_argument("--compiler-host", choices=("gfe",),
                        help="compile on the approved remote Ubuntu NSIS host")
    args = parser.parse_args(argv)

    try:
        layout = validate_layout(args.layout)
        output_file = normalize_output(args.output, args.version)
        script = Path(__file__).with_name("installer.nsi").resolve()
        if not VERSION_RE.fullmatch(args.version):
            raise BuildInputError(f"Invalid semantic version: {args.version}")
        if args.compiler_host == "gfe":
            print("Compiling Windows installer on gfe with Ubuntu NSIS 3.10", flush=True)
            remote_compile(layout, script, args.version, output_file)
        else:
            if not args.makensis:
                raise BuildInputError("--makensis is required unless --compiler-host gfe is selected")
            command = compiler_command(args.makensis, script, layout, args.version, output_file)
            subprocess.run(command, check=True)
    except (BuildInputError, OSError, subprocess.CalledProcessError) as exc:
        print(f"Windows installer build failed: {exc}", file=sys.stderr)
        return 1

    if not output_file.is_file():
        print(f"makensis completed without producing {output_file}", file=sys.stderr)
        return 1
    print(f"Built {output_file}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

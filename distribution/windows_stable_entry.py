"""Install the protocol-2 maintenance binary as the stable Windows root entry."""
import json
from pathlib import Path
import shutil


def install_stable_entry(layout: Path) -> None:
    broker = layout / 'maintenance' / 'StarTournamentMaintenance.exe'
    contract = layout / 'maintenance' / 'maintenance.json'
    entry = layout / 'Star Tournament.exe'
    for path in (layout, layout / 'maintenance', broker, contract, entry):
        if path.is_symlink():
            raise ValueError('Stable Windows entry cannot use symlinks')
    if not broker.is_file() or broker.stat().st_size == 0 or not entry.is_file():
        raise ValueError('Complete Windows bootstrap and maintenance binary required')
    if json.loads(contract.read_text())['protocol'] != 2:
        raise ValueError('Stable Windows entry requires maintenance protocol 2')
    shutil.copyfile(broker, entry)

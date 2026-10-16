import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('windows_stable_entry',Path(__file__).resolve().parents[1]/'windows_stable_entry.py')
module=importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class StableEntryTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name);(self.root/'maintenance').mkdir()
        self.entry=self.root/'Star Tournament.exe';self.entry.write_bytes(b'generated stub')
        self.broker=self.root/'maintenance/StarTournamentMaintenance.exe';self.broker.write_bytes(b'exact stable broker')
        self.contract=self.root/'maintenance/maintenance.json';self.contract.write_text(json.dumps({'protocol':2}))
    def test_exact_stable_bytes_replace_generated_stub(self):
        module.install_stable_entry(self.root)
        self.assertEqual(self.entry.read_bytes(),self.broker.read_bytes())
    def test_old_protocol_is_rejected_before_replacement(self):
        self.contract.write_text(json.dumps({'protocol':1}))
        with self.assertRaises(ValueError):module.install_stable_entry(self.root)
        self.assertEqual(self.entry.read_bytes(),b'generated stub')
    def test_missing_broker_preserves_entry(self):
        self.broker.unlink()
        with self.assertRaises(ValueError):module.install_stable_entry(self.root)
        self.assertEqual(self.entry.read_bytes(),b'generated stub')
    def test_no_redirected_bootstrap_paths(self):
        self.entry.unlink();self.entry.symlink_to(self.broker)
        with self.assertRaises(ValueError):module.install_stable_entry(self.root)
        self.assertEqual(self.broker.read_bytes(),b'exact stable broker')
    def test_empty_broker_is_not_installed(self):
        self.broker.write_bytes(b'')
        with self.assertRaises(ValueError):module.install_stable_entry(self.root)
        self.assertEqual(self.entry.read_bytes(),b'generated stub')
